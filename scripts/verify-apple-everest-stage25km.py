#!/usr/bin/env python3
"""Check a newly regenerated K-M result against the reviewed sanitized ledger."""
import argparse
import importlib.util
import json
from pathlib import Path
import sys
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('km_audit',ROOT/'scripts/audit-apple-everest-stage25km.py')
km=importlib.util.module_from_spec(spec);sys.modules[spec.name]=km;spec.loader.exec_module(km)
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--result',type=Path,required=True)
    p.add_argument('--compare',type=Path,default=ROOT/'apple-everest/sj-beginner-expansion-stage25km.json');a=p.parse_args()
    result=km.load(a.result);expected=km.load(a.compare)
    km.validate_result(result);km.validate_result(expected)
    km.require(result['inputBindingsSha256']==km.file_sha(ROOT/'apple-everest/sj-beginner-expansion-inputs-stage25km.json'),'input authority changed')
    km.require(result['decisionBindingsSha256']==km.file_sha(ROOT/'apple-everest/sj-beginner-expansion-decisions-stage25km.json'),'reviewed decisions changed')
    km.require(result==expected,'fresh complete audit differs from reviewed ledger')
    print('PASS: complete regenerated K-M ledger matches; audit is not gameplay acceptance')
if __name__=='__main__':main()
