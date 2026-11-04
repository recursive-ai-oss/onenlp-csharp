"""Probes which UD features the shared-task evaluation script compares, by running it (as a black box) on crafted one-word files.

usage: probe_feats.py HOME    (HOME as for crosscheck.py)
"""
import sys, io
sys.path.insert(0, sys.argv[1] + "/eval/ud/tools")
import conll18_ud_eval as ref

def load(feats):
    return ref.load_conllu(io.StringIO("1\tx\tx\tX\tX\t%s\t0\troot\t_\t_\n\n" % feats))

# every feature name published in the UD guidelines' feature inventory that we could think of, plus layered forms
names = """Abbr Animacy Aspect Case Clusivity Definite Degree Echo Evident Foreign Gender Hyph Mood NameType NounClass NounType NumForm
NumType NumValue Number Person Polarity Polite Poss PronType Reflex Style Subcat Tense Typo VerbForm VerbType Voice ExtPos Variant
Number[psor] Gender[psor] Person[psor] Case[abs] Number[erg] Person[dat] Gender[obj] Foo Lang Translit LTranslit Compound""".split()
ignored = []
for n in names:
    f = "%s=Yes" % n
    if ref.evaluate(load("Case=Nom|" + f), load("Case=Nom"))["UFeats"].f1 == 1.0:
        ignored.append(n)
print("ignored feature names:", ignored)
# does an ignored name with a different value count? does it matter if only the other side has it?
print("both sides, different values:", ref.evaluate(load("Typo=Yes"), load("Typo=No"))["UFeats"].f1)
print("system only:", ref.evaluate(load("Case=Nom"), load("Case=Nom|Typo=Yes"))["UFeats"].f1)
print("order:", ref.evaluate(load("Case=Nom|Number=Sing"), load("Number=Sing|Case=Nom"))["UFeats"].f1)
print("layered bracket:", ref.evaluate(load("Case=Nom"), load("Case=Nom|Number[psor]=Sing"))["UFeats"].f1)
