"""Regression tests for native verifier stdout/stderr separation; no Apple tools needed."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1] / 'scripts'
PARSERS = {'verify-ios-native.py': 'exports', 'verify-tvos-artifacts.py': 'exported_symbols'}
RUNNER = '''
import importlib.util,json,pathlib,subprocess,sys
spec=importlib.util.spec_from_file_location('verifier',sys.argv[1])
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
try: symbols=sorted(getattr(m,sys.argv[2])(pathlib.Path(sys.argv[3])))
except subprocess.CalledProcessError as error: raise SystemExit(error.returncode)
print(json.dumps(symbols))
if set(json.loads(sys.argv[4]))-set(symbols):raise SystemExit(17)
'''
FAKE_XCRUN = '''
import json,os,sys
case=json.load(open(sys.argv[-1]))
for fd,text in case['writes']:os.write(fd,text.encode())
raise SystemExit(case.get('exit',0))
'''

class NativeSymbolStreams(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        fake = self.root / 'xcrun'
        fake.write_text('#!' + sys.executable + '\n' + FAKE_XCRUN)
        fake.chmod(0o700)
        self.env = dict(os.environ, PATH=str(self.root)+os.pathsep+os.environ.get('PATH',''), PYTHONDONTWRITEBYTECODE='1')

    def invoke(self, script, writes, required=(), exit_code=0):
        case = self.root / 'case.json'
        case.write_text(json.dumps({'writes':writes,'exit':exit_code}))
        return subprocess.run([sys.executable,'-c',RUNNER,str(SCRIPTS/script),PARSERS[script],str(case),json.dumps(required)],env=self.env,capture_output=True,text=True)

    def test_normal_valid_output(self):
        for script in PARSERS:
            with self.subTest(script=script):
                result = self.invoke(script,[(1,'archive(member.o):\n_Required\n__Double\n_Required\n')],['Required'])
                self.assertEqual(result.returncode,0,result.stderr)
                self.assertEqual(json.loads(result.stdout),['Required','_Double'])
                self.assertEqual(result.stderr,'')

    def test_warning_between_partial_stdout_writes(self):
        for script in PARSERS:
            with self.subTest(script=script):
                result = self.invoke(script,[(1,'_SDL_Enclo'),(2,'warning: no symbols\n'),(1,'sePoints\n')],['SDL_EnclosePoints'])
                self.assertEqual(result.returncode,0,result.stderr)
                self.assertEqual(json.loads(result.stdout),['SDL_EnclosePoints'])
                self.assertIn('warning: no symbols',result.stderr)

    def test_genuinely_missing_symbol_fails(self):
        for script in PARSERS:
            with self.subTest(script=script):
                result = self.invoke(script,[(1,'_Other\n')],['Required'])
                self.assertEqual(result.returncode,17)
                self.assertNotIn('Required',json.loads(result.stdout))

    def test_symbol_only_in_stderr_does_not_satisfy_requirement(self):
        for script in PARSERS:
            with self.subTest(script=script):
                result = self.invoke(script,[(1,'_Other\n'),(2,'_Required\n')],['Required'])
                self.assertEqual(result.returncode,17)
                self.assertEqual(json.loads(result.stdout),['Other'])
                self.assertIn('_Required',result.stderr)

    def test_nonzero_nm_exit_is_preserved(self):
        for script in PARSERS:
            with self.subTest(script=script):
                result = self.invoke(script,[(1,'_Required\n'),(2,'fatal nm diagnostic\n')],['Required'],23)
                self.assertEqual(result.returncode,23)
                self.assertEqual(result.stdout,'')
                self.assertIn('fatal nm diagnostic',result.stderr)

    def test_warning_free_inventory_is_unchanged(self):
        inventory = ['OC_DCT_TOKEN_EXTRA_BITS','SDL_EnclosePoints','SDL_RenderFillRectF','th_decode_packetin','tf_open_callbacks']
        writes = [(1,'archive(member.o):\n'+''.join('_'+s+'\n' for s in inventory))]
        for script in PARSERS:
            with self.subTest(script=script):
                result = self.invoke(script,writes,inventory)
                self.assertEqual(result.returncode,0,result.stderr)
                self.assertEqual(json.loads(result.stdout),sorted(inventory))
                self.assertEqual(result.stderr,'')

if __name__=='__main__':unittest.main()
