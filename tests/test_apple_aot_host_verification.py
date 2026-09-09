"""Portable, owned fixtures for real provenance/platform/signing entry points."""
import copy
import importlib.util
import json
from pathlib import Path
import plistlib
import shlex
import struct
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('product', ROOT / 'scripts/verify-apple-everest-aot-factory-product.py')
product = importlib.util.module_from_spec(spec)
spec.loader.exec_module(product)
p = product.provenance


def native(platform='ios', signed=False):
    commands = struct.pack('<6I', 0x32, 24, 2 if platform == 'ios' else 3,
                           (15 if platform == 'ios' else 16) << 16, 26 << 16 | 5 << 8, 0)
    commands += struct.pack('<6I', 0x1d if signed else 0x777, 24, 0, 0, 0, 0)
    return struct.pack('<8I', 0xfeedfacf, 0x100000c, 0, 2, 2, len(commands), 0, 0) + commands


class HostVerification(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.build = self.root / 'build'
        self.proof = self.build / 'aot-provenance'
        self.proof.mkdir(parents=True)
        self.app = self.root / 'Fixture.app'
        self.app.mkdir()
        self.source = {'sourceCommit': '1' * 40, 'sourceTreeDirty': False}

    def fixture(self, host='x64', platform='ios'):
        self.host, self.target = host, platform
        dotnet = self.root / 'sdk/dotnet'
        dotnet.parent.mkdir(exist_ok=True)
        dotnet.write_bytes(b'owned dotnet fixture')
        self.compiler = dotnet.parent / 'packs' / ('Microsoft.NETCore.App.Runtime.AOT.osx-' + host + '.Cross.' + platform + '-arm64') / '10.0.10/tools/mono-aot-cross'
        self.compiler.parent.mkdir(parents=True, exist_ok=True)
        for name in ('mono-aot-cross', 'llc', 'opt'):
            (self.compiler.parent / name).write_bytes(('owned ' + name).encode())
        self.observed = {'machine': 'x86_64' if host == 'x64' else 'arm64', 'hostArchitecture': host, 'dotnet': str(dotnet)}
        self.obj = self.build / ('obj/Fixture/release_' + platform + '-arm64')
        self.obj.mkdir(parents=True, exist_ok=True)
        self.fields = {}
        for name in ('Celeste', 'DJMapHelper'):
            row = {'Abi': 'ARM64+LLVM', 'Arch': 'arm64', 'ProcessArguments': '--llvm',
                   'LinkedAssembly': str(self.obj / (name + '.dll')),
                   'ObjectFile': str(self.obj / (name + '.dll.o')),
                   'LLVMFile': str(self.obj / (name + '.dll.llvm.o')),
                   'AOTData': str(self.obj / (name + '.aotdata')),
                   'AOTAssembly': str(self.obj / (name + '.dll.s'))}
            row['Arguments'] = shlex.join(['--aot=mtriple=arm64-ios', 'full', 'static', 'asmonly',
                'llvm-path=' + str(self.compiler.parent), 'data-outfile=' + row['AOTData'],
                'outfile=' + row['AOTAssembly'], 'llvm-outfile=' + row['LLVMFile']])
            Path(row['LinkedAssembly']).write_bytes(('owned linked ' + name).encode())
            self.fields[name] = row
        xml = ET.Element('Project')
        for row in self.fields.values():
            item = ET.SubElement(xml, '_AssembliesToAOT', Include=row['LinkedAssembly'])
            for key, value in row.items():
                if key != 'LinkedAssembly': ET.SubElement(item, key).text = value
        ET.ElementTree(xml).write(self.proof / 'compiler-items.xml')
        self.main_file = self.obj / 'main.arm64.mm'
        self.main_file.write_text('owned native registration fixture')
        self.info = {'CFBundleSupportedPlatforms': ['iPhoneOS' if platform == 'ios' else 'AppleTVOS'],
                     'UIDeviceFamily': [1, 2] if platform == 'ios' else [3],
                     'DTPlatformName': 'iphoneos' if platform == 'ios' else 'appletvos',
                     'MinimumOSVersion': '15.0' if platform == 'ios' else '16.0',
                     'DTPlatformVersion': '26.5', 'CFBundleExecutable': 'Fixture'}
        self.info['DTSDKName'] = self.info['DTPlatformName'] + '26.5'
        (self.app / 'Info.plist').write_bytes(plistlib.dumps(self.info))
        (self.app / 'Fixture').write_bytes(native(platform))

    def tool(self, command, **kwargs):
        if command[:3] == ['xcrun', 'lipo', '-archs']: return self.observed['machine'] + '\n'
        if command == [str(self.compiler), '--version']:
            return 'Mono JIT compiler version 10.0.10.0 (fixture)\n Architecture: arm64\n'
        raise AssertionError(command)

    def boundary(self, phase, sdk_case=True):
        args = ['receipt', phase, '--root', str(self.proof)]
        if phase == 'before':
            target = {'ios': 'iOS', 'tvos': 'tvOS'}[self.target] if sdk_case else self.target
            args += ['--compiler', str(self.compiler), '--platform', target, '--native-main', str(self.main_file)]
        if phase == 'native': args += ['--native', str(self.app / 'Fixture')]
        with patch.object(p, 'observe_toolchain', return_value=self.observed), patch.object(p, 'source_identity', return_value=self.source), patch.object(p.subprocess, 'check_output', side_effect=self.tool), patch('sys.argv', args):
            p.main()
        return json.loads((self.proof / 'receipt.json').read_text())

    def completed(self):
        record = self.boundary('before')
        for row in self.fields.values():
            for key in ('ObjectFile', 'LLVMFile', 'AOTData'): Path(row[key]).write_bytes(('owned ' + key).encode())
        self.boundary('after')
        return self.boundary('native')

    def verify(self, record, fields=None, info=None, data=None):
        with patch.object(p, 'observe_toolchain', return_value=self.observed), patch.object(p.subprocess, 'check_output', side_effect=self.tool):
            return product.verify_platform(info or self.info, fields or self.fields['Celeste'], record, data or native(self.target))

    def test_both_hosts_and_device_platforms_and_existing_controls(self):
        for host in ('x64', 'arm64'):
            for target in ('ios', 'tvos'):
                with self.subTest(host=host, target=target), tempfile.TemporaryDirectory() as directory:
                    old = self.root; self.root = Path(directory).resolve(); self.proof = self.root / 'proof'; self.proof.mkdir()
                    self.build = self.root / 'build'
                    self.fixture(host, target)
                    record = self.completed()
                    self.assertEqual(self.verify(record), target)
                    with patch.object(p, 'observe_toolchain', return_value=self.observed), patch.object(p.subprocess, 'check_output', side_effect=self.tool):
                        result = product.platform_controls(self.info, self.fields['Celeste'], record, native(target))
                    self.assertEqual(len(result['rejectedControls']), 15)
                    self.root = old

    def test_pack_receipt_and_llvm_rejections(self):
        self.fixture(); record = self.completed()
        mutations = [lambda r: r.update(compiler=r['compiler'].replace('osx-x64', 'osx-arm64')),
                     lambda r: r.update(compiler=r['compiler'].replace('Cross.ios-arm64', 'Cross.ios-x64')),
                     lambda r: r.update(compiler=r['compiler'].replace('Cross.ios', 'Cross.tvos')),
                     lambda r: r.update(compiler=r['compiler'].replace('10.0.10', '10.0.11')),
                     lambda r: r.update(compilerSha256='0' * 64),
                     lambda r: r.update(targetArchitecture='x64'),
                     lambda r: r.update(targetPlatform='tvos'),
                     lambda r: r['toolchain'].update(hostArchitecture='arm64'),
                     lambda r: r['llvmSha256'].update(llc='0' * 64),
                     lambda r: r.pop('toolchain')]
        for mutate in mutations:
            value = copy.deepcopy(record); mutate(value)
            with self.subTest(mutation=mutate), self.assertRaises((ValueError, KeyError)): self.verify(value)
        fields = dict(self.fields['Celeste']); fields['Arguments'] = fields['Arguments'].replace('llvm-path=', 'llvm-path=/wrong/')
        with self.assertRaises(ValueError): self.verify(record, fields)
        for name in ('mono-aot-cross', 'llc', 'opt'):
            path = self.compiler.parent / name; before = path.read_bytes(); path.write_bytes(b'tampered')
            with self.subTest(tool=name), self.assertRaises(ValueError): self.verify(record)
            path.write_bytes(before)
        (self.compiler.parent / 'opt').unlink()
        with self.assertRaises(FileNotFoundError): self.verify(record)

    def test_boundaries_reject_source_input_items_and_compiler_changes(self):
        self.fixture(); original = self.boundary('before'); receipt = self.proof / 'receipt.json'
        for name, mutation in [
                ('source', lambda r: r.update(sourceCommit='2' * 40)),
                ('dirty', lambda r: r.update(sourceTreeDirty=True)),
                ('items', lambda r: r.update(itemsSha256='0' * 64)),
                ('input', lambda r: r['inputHashes'].update(LinkedAssembly='0' * 64)),
                ('compiler', lambda r: r.update(compilerSha256='0' * 64)),
                ('companion', lambda r: r['companions']['DJMapHelper']['fields'].update(Arch='x64'))]:
            value = copy.deepcopy(original); mutation(value); receipt.write_text(json.dumps(value))
            with self.subTest(name=name), self.assertRaises(ValueError): self.boundary('after')
        receipt.unlink()
        with self.assertRaises(FileNotFoundError): self.boundary('after')

    def test_stale_and_missing_outputs_rejected(self):
        self.fixture(); self.boundary('before')
        with self.assertRaises(ValueError): self.boundary('before')
        with self.assertRaises(FileNotFoundError): self.boundary('after')

    def test_lowercase_cli_target_is_retained(self):
        self.fixture()
        self.assertEqual(self.boundary('before', sdk_case=False)['targetPlatform'], 'ios')

    def test_actual_compiler_version_architecture_and_probe_failures(self):
        self.fixture(); record = self.completed()
        for answer in ('Mono JIT compiler version 10.0.10.0 (fixture)\n Architecture: x64\n', 'Mono JIT compiler version 10.0.11.0 (fixture)\n Architecture: arm64\n'):
            with patch.object(p, 'observe_toolchain', return_value=self.observed), patch.object(p.subprocess, 'check_output', side_effect=lambda cmd, **kw: self.observed['machine'] if cmd[0] == 'xcrun' else answer), self.assertRaises(ValueError):
                product.verify_platform(self.info, self.fields['Celeste'], record, native())
        with patch.object(p, 'observe_toolchain', return_value=self.observed), patch.object(p.subprocess, 'check_output', side_effect=subprocess.CalledProcessError(2, ['probe'])), self.assertRaises(subprocess.CalledProcessError):
            product.verify_platform(self.info, self.fields['Celeste'], record, native())

    def test_full_static_llvm_and_argument_mapping_remain_required(self):
        self.fixture(); path = self.proof / 'compiler-items.xml'; original = path.read_text()
        for token in ('full', 'static', 'asmonly', '--llvm', 'data-outfile=', 'llvm-outfile='):
            path.write_text(original.replace(token, 'wrong', 1))
            with self.subTest(token=token), self.assertRaises(ValueError): p.selected_item(path)


    def test_product_entry_runs_linked_proofs_in_both_modes(self):
        self.fixture(); self.completed()
        items = self.obj / 'linker-items/_AssembliesToAOT.items'
        items.parent.mkdir(); items.write_bytes((self.proof / 'compiler-items.xml').read_bytes())
        for relative in ('stripped/Celeste.dll', 'nativelibraries/Fixture', 'linker-cache/main.arm64.mm'):
            path = self.obj / relative; path.parent.mkdir(exist_ok=True); path.write_bytes(b'owned evidence')
        (self.app / 'Celeste.dll').write_bytes(b'owned packaged assembly')
        (self.app / 'Celeste.aotdata.arm64').write_bytes(b'owned packaged aot data')
        for mode in ('development', 'unsigned'):
            calls = []
            def execute(command, **kwargs):
                calls.append(command)
                if command[:2] == ['codesign', '-d']:
                    return subprocess.CompletedProcess(command, 1, '', str(self.app) + ': code object is not signed at all\n')
                return subprocess.CompletedProcess(command, 0)
            args = ['verify', '--build', str(self.build), '--app', str(self.app), '--manifest', 'owned.json',
                    '--authored-profiles', 'owned-profiles.json', '--output', str(self.root / 'result.json')]
            if mode == 'unsigned': args += ['--signing', mode]
            with self.subTest(mode=mode), patch('sys.argv', args), patch.object(p, 'observe_toolchain', return_value=self.observed), patch.object(p, 'source_identity', return_value=self.source), patch.object(p.subprocess, 'check_output', side_effect=self.tool), patch.object(product.subprocess, 'run', side_effect=execute):
                product.main()
            self.assertEqual(len([call for call in calls if 'verify-aot-factory-product' in call]), 1)
            self.assertEqual(calls[0][:2], ['codesign', '-d' if mode == 'unsigned' else '--verify'])
        self.source['sourceCommit'] = '3' * 40
        with patch('sys.argv', args), patch.object(p, 'observe_toolchain', return_value=self.observed), patch.object(p, 'source_identity', return_value=self.source), patch.object(p.subprocess, 'check_output', side_effect=self.tool), patch.object(product.subprocess, 'run') as run, self.assertRaises(ValueError):
            product.main()
        run.assert_not_called()


class ToolchainObservation(unittest.TestCase):
    def test_independent_observation_and_wrong_host_tool_versions(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve(); dotnet = root / 'dotnet'; dotnet.write_bytes(b'owned tool')
            for host in ('x64', 'arm64'):
                machine = 'x86_64' if host == 'x64' else 'arm64'
                text = ('.NET SDK:\n Version: 10.0.302\n Workload version: 10.0.302.0\n\n'
                        'Runtime Environment:\n RID: osx-' + host + '\n Base Path: ' + str(root / 'sdk/10.0.302') + '\n\n'
                        'Host:\n Version: 10.0.10\n Architecture: ' + host + '\n')
                def invoke(command, **kwargs):
                    if command[-1] == '--info': return current
                    if command[0] == 'xcodebuild': return 'Xcode 26.6\nBuild version 17F113\n'
                    if command[0] == 'xcrun': return '26.5\n'
                    raise AssertionError(command)
                variants = [text, text.replace('10.0.302\n', '10.0.303\n'),
                            text.replace('10.0.302.0', '10.0.301.0'), text.replace('10.0.10\n', '10.0.11\n'),
                            text.replace('RID: osx-' + host, 'RID: osx-unknown'),
                            text.replace('Architecture: ' + host, 'Architecture: unknown'),
                            text.replace(str(root / 'sdk/10.0.302'), str(root / 'other/sdk/10.0.302')),
                            text.replace('Base Path:', 'Missing:')]
                with patch.object(p.platform, 'system', return_value='Darwin'), patch.object(p.platform, 'machine', return_value=machine), patch.object(p.shutil, 'which', return_value=str(dotnet)), patch.object(p.subprocess, 'check_output', side_effect=invoke):
                    for index, current in enumerate(variants):
                        with self.subTest(host=host, variant=index):
                            if index == 0: self.assertEqual(p.observe_toolchain()['hostArchitecture'], host)
                            else:
                                with self.assertRaises(ValueError): p.observe_toolchain()
                    current = text
                    with patch.object(p.subprocess, 'check_output', side_effect=subprocess.CalledProcessError(3, ['dotnet'])), self.assertRaises(subprocess.CalledProcessError):
                        p.observe_toolchain()


class SigningVerification(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.app = Path(self.temp.name).resolve() / 'Fixture.app'; self.app.mkdir()
        (self.app / 'Fixture').write_bytes(native())

    def unsigned(self):
        return subprocess.CompletedProcess([], 1, '', str(self.app) + ': code object is not signed at all\n')

    def test_signed_is_strict_and_failure_is_never_retried(self):
        with patch.object(product.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0)) as run:
            self.assertEqual(product.verify_signing(self.app, 'development'), 'SIGNED_STRICT_VERIFIED')
            run.assert_called_once_with(['codesign', '--verify', '--strict', str(self.app)], check=True)
        with patch.object(product.subprocess, 'run', side_effect=subprocess.CalledProcessError(1, ['codesign'])) as run:
            with self.assertRaises(subprocess.CalledProcessError): product.verify_signing(self.app, 'development')
            self.assertEqual(run.call_count, 1)

    def test_unsigned_requires_positive_absence(self):
        with patch.object(product.subprocess, 'run', return_value=self.unsigned()):
            self.assertEqual(product.verify_signing(self.app, 'unsigned'), 'UNSIGNED_ABSENCE_VERIFIED')
        for result in (subprocess.CompletedProcess([], 0, '', 'Signature=adhoc'),
                       subprocess.CompletedProcess([], 1, '', 'unrelated tool failure'),
                       subprocess.CompletedProcess([], 2, '', self.unsigned().stderr),
                       subprocess.CompletedProcess([], 1, 'unexpected', self.unsigned().stderr)):
            with self.subTest(result=result), patch.object(product.subprocess, 'run', return_value=result), self.assertRaises(ValueError):
                product.verify_signing(self.app, 'unsigned')

    def test_signatures_provisioning_nested_material_and_malformed_native_fail(self):
        for name in ('embedded.mobileprovision', '_CodeSignature/CodeResources', 'nested/test.xcent', 'nested/test.entitlements'):
            file = self.app / name; file.parent.mkdir(exist_ok=True); file.write_bytes(b'owned fixture')
            with self.subTest(name=name), self.assertRaises(ValueError): product.verify_signing(self.app, 'unsigned')
            file.unlink()
            if file.parent != self.app: file.parent.rmdir()
        for data in (native(signed=True), native()[:34], b'\xca\xfe\xba\xbe'):
            (self.app / 'nested').write_bytes(data)
            with self.subTest(data=data[:4]), self.assertRaises(ValueError): product.verify_signing(self.app, 'unsigned')

    def test_empty_archived_entitlements_still_fail_unsigned(self):
        # The SDK can emit this empty file from a declared Entitlements.plist
        # even with EnableCodeSigning=false. Fix the unsigned caller, not the gate.
        (self.app / 'archived-expanded-entitlements.xcent').write_bytes(plistlib.dumps({}))
        with patch.object(product.subprocess, 'run') as run, self.assertRaises(ValueError):
            product.verify_signing(self.app, 'unsigned')
        run.assert_not_called()


if __name__ == '__main__': unittest.main()
