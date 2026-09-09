"""Exercise production required-symbol/stub gates through controlled nm streams."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
RUNNER = r'''
import importlib.util,json,pathlib,subprocess,sys
root=pathlib.Path(sys.argv[1]); cases=pathlib.Path(sys.argv[2]); platform=sys.argv[3]
script='verify-ios-native.py' if platform=='ios' else 'verify-tvos-artifacts.py'
spec=importlib.util.spec_from_file_location('verifier',root/'scripts'/script)
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
config=json.loads((cases/'config.json').read_text())
try:
    inventories={c:m.collect_exports(cases/(c+'.json'),config['architectures'][c]) for c in m.COMPONENTS}
    if platform=='tvos':
        expected=json.loads((root/'native/tvos-symbol-expectations.json').read_text())
        m.validate_required_symbols(config['variant'],inventories,expected)
    else:
        for c,values in inventories.items():
            m.validate_component_symbols(c,values,['FNA3D_Driver_Metal.o'])
        m.validate_stub_overlap(inventories)
except subprocess.CalledProcessError as error:
    raise SystemExit(error.returncode)
print('PASS: actual production symbol gates')
'''
FAKE_NM = r'''
import json,os,sys
assert sys.argv[1]=='nm' and '-arch' in sys.argv
arch=sys.argv[sys.argv.index('-arch')+1]
case=json.load(open(sys.argv[-1]))
if arch not in case:
    os.write(2,b'requested architecture absent\n');raise SystemExit(24)
for fd,text in case[arch]['writes']:os.write(fd,text.encode())
raise SystemExit(case[arch].get('exit',0))
'''


class ArchitectureSymbolTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.path = Path(temporary.name)
        fake = self.path / "xcrun"
        fake.write_text("#!" + sys.executable + "\n" + FAKE_NM)
        fake.chmod(0o700)
        self.env = dict(os.environ, PATH=str(self.path) + os.pathsep + os.environ.get("PATH", ""),
                        PYTHONDONTWRITEBYTECODE="1")
        self.expected = json.loads((ROOT / "native/tvos-symbol-expectations.json").read_text())

    def cases(self, platform="tvos", variant="simulator"):
        if platform == "tvos":
            symbols = {c: set(v["symbols"]) for c, v in self.expected["components"].items()}
            symbols["SDL2"] -= symbols["tvStubs"]
        else:
            import importlib.util
            spec = importlib.util.spec_from_file_location("ios", ROOT / "scripts/verify-ios-native.py")
            ios = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(ios)
            symbols = {c: set() for c in ios.COMPONENTS}
            symbols["SDL2"] = {"SDL_UIKitRunApp"}
            symbols["ApplePlatformStubs"] = ios.STUB_SYMBOLS
        architectures, cases = {}, {}
        for component, names in symbols.items():
            archs = ["arm64", "x86_64"] if platform == "tvos" and variant == "simulator" else ["arm64"]
            if platform == "tvos" and variant == "device" and component == "MoltenVK":
                archs.append("arm64e")
            architectures[component] = archs
            cases[component] = {a: {"writes": [(1, "".join("_" + n + "\n" for n in sorted(names)))]} for a in archs}
        return {"variant": variant, "architectures": architectures}, cases

    def invoke(self, config, cases, platform="tvos"):
        (self.path / "config.json").write_text(json.dumps(config))
        for component, case in cases.items():
            (self.path / (component + ".json")).write_text(json.dumps(case))
        return subprocess.run([sys.executable, "-c", RUNNER, str(ROOT), str(self.path), platform],
                              env=self.env, capture_output=True, text=True)

    def remove(self, cases, component, arch, symbol):
        fd, text = cases[component][arch]["writes"][0]
        self.assertIn("_" + symbol + "\n", text)
        cases[component][arch]["writes"][0] = (fd, text.replace("_" + symbol + "\n", ""))

    def test_valid_complete_inventories(self):
        for platform, variant in (("ios", "device"), ("tvos", "device"), ("tvos", "simulator")):
            with self.subTest(platform=platform, variant=variant):
                result = self.invoke(*self.cases(platform, variant), platform)
                self.assertEqual(result.returncode, 0, result.stderr)

    def test_missing_arm64_never_falls_back(self):
        for platform in ("ios", "tvos"):
            config, cases = self.cases(platform)
            config["architectures"]["SDL2"] = ["x86_64"]
            result = self.invoke(config, cases, platform)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("arm64", result.stderr)

    def test_nm_missing_arm64_despite_metadata_fails(self):
        config, cases = self.cases()
        del cases["SDL2"]["arm64"]
        result = self.invoke(config, cases)
        self.assertEqual(result.returncode, 24)
        self.assertIn("architecture absent", result.stderr)

    def test_missing_x86_required_symbol_is_not_hidden_by_arm64(self):
        config, cases = self.cases()
        self.remove(cases, "SDL2", "x86_64", "SDL_Init")
        result = self.invoke(config, cases)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("missing SDL2/simulator/x86_64 expected symbols", result.stderr)

    def test_one_slice_cannot_supply_anothers_stub(self):
        config, cases = self.cases()
        symbol = self.expected["components"]["tvStubs"]["symbols"][0]
        self.remove(cases, "tvStubs", "x86_64", symbol)
        result = self.invoke(config, cases)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("/simulator/x86_64 expected symbols", result.stderr)

    def test_stderr_cannot_supply_required_symbol(self):
        for platform in ("ios", "tvos"):
            config, cases = self.cases(platform)
            arch, symbol = ("arm64", "SDL_UIKitRunApp") if platform == "ios" else ("x86_64", "SDL_Init")
            self.remove(cases, "SDL2", arch, symbol)
            cases["SDL2"][arch]["writes"].append((2, "_" + symbol + "\n"))
            result = self.invoke(config, cases, platform)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("_" + symbol, result.stderr)

    def test_architecture_specific_nonzero_nm_failure_propagates(self):
        config, cases = self.cases()
        cases["SDL2"]["x86_64"].update(exit=27)
        cases["SDL2"]["x86_64"]["writes"].append((2, "fatal x86_64 diagnostic\n"))
        result = self.invoke(config, cases)
        self.assertEqual(result.returncode, 27)
        self.assertIn("fatal x86_64 diagnostic", result.stderr)

    def test_warning_between_partial_writes_preserves_real_gate(self):
        config, cases = self.cases()
        self.remove(cases, "SDL2", "x86_64", "SDL_Init")
        cases["SDL2"]["x86_64"]["writes"].extend([(1, "_SDL_In"), (2, "warning: no symbols\n"), (1, "it\n")])
        result = self.invoke(config, cases)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("warning: no symbols", result.stderr)

    def test_stub_overlap_on_x86_is_rejected(self):
        config, cases = self.cases()
        symbol = self.expected["components"]["tvStubs"]["symbols"][0]
        cases["FAudio"]["x86_64"]["writes"].append((1, "_" + symbol + "\n"))
        result = self.invoke(config, cases)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("tvStubs shadows real simulator/x86_64", result.stderr)

    def test_moltenvk_arm64e_has_its_own_requirement(self):
        config, cases = self.cases(variant="device")
        symbol = self.expected["components"]["MoltenVK"]["symbols"][0]
        self.remove(cases, "MoltenVK", "arm64e", symbol)
        result = self.invoke(config, cases)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("missing MoltenVK/device/arm64e expected symbols", result.stderr)


if __name__ == "__main__":
    unittest.main()
