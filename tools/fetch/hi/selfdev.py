#!/usr/bin/env python3
"""Writes testdata/hi/selfdev.conllu: Hindi sentences that we annotated by hand (17 from web text, the rest written by us) (UPOS, head, relation) in the UD Hindi style.

The sentences are from the FineWeb-2 Hindi sample (ODC-By 1.0) used for labelling, picked by position in a pilot slice, not by
difficulty. The set is small on purpose: it ranks the source languages of the labeller and nothing else (Hindi-PUD is evaluation only).
One line per sentence: form/UPOS/head/relation, head 0 = root.
"""
import sys

S = [
"चीन/PROPN/8/obl में/ADP/1/case एक/NUM/4/nummod समय/NOUN/8/obl:tmod चोरों/NOUN/8/nmod का/ADP/5/case बड़ा/ADJ/8/amod आतंक/NOUN/0/root था/AUX/8/cop ./PUNCT/8/punct",
"अब/ADV/6/advmod मेरे/PRON/3/nmod राज्य/NOUN/5/nmod की/ADP/3/case सुरक्षा/NOUN/6/nsubj खतरे/NOUN/0/root में/ADP/6/case है/AUX/6/cop ./PUNCT/6/punct ”/PUNCT/6/punct",
"इस/DET/2/det बीच/NOUN/11/obl ,/PUNCT/11/punct सीएजी/PROPN/11/nsubj ने/ADP/4/case पीएमओ/PROPN/11/obl को/ADP/6/case चिट्ठी/NOUN/9/obj लिखकर/VERB/11/advcl सफाई/NOUN/11/obj दी/VERB/0/root है/AUX/11/aux ।/PUNCT/11/punct",
"इस/DET/2/det दौरान/NOUN/6/obl:tmod शिबू/PROPN/6/nsubj सोरेन/PROPN/3/flat कोयला/NOUN/6/compound मंत्री/NOUN/0/root थे/AUX/6/cop ।/PUNCT/6/punct",
"संसद/NOUN/3/nmod में/ADP/1/case हंगामे/NOUN/13/obl के/ADP/3/case बाद/ADP/3/case कांग्रेस/PROPN/13/nsubj '/PUNCT/9/punct डैमेज/NOUN/9/compound कंट्रोल/NOUN/11/obj '/PUNCT/9/punct करने/VERB/13/advcl में/ADP/11/case जुट/VERB/0/root गई/AUX/13/aux है/AUX/13/aux ।/PUNCT/13/punct",
"कांग्रेस/PROPN/3/nmod के/ADP/1/case नेता/NOUN/7/nsubj राजीव/PROPN/3/appos शुक्ला/PROPN/4/flat ने/ADP/3/case कहा/VERB/0/root है/AUX/7/aux कि/SCONJ/13/mark कोई/DET/11/det घोटाला/NOUN/13/nsubj नहीं/PART/13/advmod हुआ/VERB/7/ccomp है/AUX/13/aux ।/PUNCT/7/punct",
"यहीं/ADV/12/advmod नही/PART/12/advmod इसके/PRON/4/nmod बीजों/NOUN/6/nmod का/ADP/4/case फैलाव/NOUN/12/nsubj:pass पक्षियों/NOUN/12/obl विशेषकर/ADV/10/advmod भारतीय/ADJ/10/amod मैना/NOUN/7/appos द्वारा/ADP/7/case किया/VERB/0/root जाता/AUX/12/aux:pass है/AUX/12/aux ।/PUNCT/12/punct",
"इस/DET/3/det कुदरती/ADJ/3/amod घटना/NOUN/8/obl से/ADP/3/case धरती/NOUN/8/obl को/ADP/5/case खतरा/NOUN/8/nsubj पहुंच/VERB/0/root सकता/AUX/8/aux है/AUX/8/aux ।/PUNCT/8/punct",
"यहां/ADV/11/advmod तक/ADP/1/case कि/SCONJ/1/fixed इंसान/NOUN/6/nmod के/ADP/4/case व्यवहार/NOUN/11/obl पर/ADP/6/case भी/ADV/6/advmod इनका/PRON/10/nmod असर/NOUN/11/nsubj पड़ता/VERB/0/root है/AUX/11/aux ।/PUNCT/11/punct",
"इससे/PRON/7/obl टेलीग्राप/NOUN/3/compound प्रणाली/NOUN/5/nmod में/ADP/3/case गड़बड़ी/NOUN/7/nsubj पैदा/ADJ/7/xcomp हो/VERB/0/root गई/AUX/7/aux ।/PUNCT/7/punct",
"सुरक्षा/NOUN/3/nmod की/ADP/1/case दृष्टि/NOUN/9/obl से/ADP/3/case ऐसे/ADJ/6/amod चित्र/NOUN/9/nsubj:pass सॉफ्टवेयर/NOUN/9/obl से/ADP/7/case हटाए/VERB/0/root जाने/AUX/9/aux:pass चाहिए/AUX/9/aux ।/PUNCT/9/punct",
"सन्/NOUN/2/compound १९६३/NUM/12/obl में/ADP/2/case भारत/PROPN/5/compound सरकार/NOUN/12/nsubj ने/ADP/5/case इसे/PRON/12/obl मानद/ADJ/9/amod विश्वविद्यालय/NOUN/11/nmod का/ADP/9/case दर्जा/NOUN/12/obj दिया/VERB/0/root ।/PUNCT/12/punct",
"इस/DET/2/det जीत/NOUN/13/obl के/ADP/2/case साथ/ADP/2/case भारत/PROPN/13/nsubj ए/PROPN/5/flat ने/ADP/5/case दो/NUM/9/nummod मैचों/NOUN/11/nmod की/ADP/9/case श्रृंखला/NOUN/13/obj बराबर/ADJ/13/xcomp कर/VERB/0/root दी/AUX/13/aux ।/PUNCT/13/punct",
"उन्होंने/PRON/13/nsubj 63/NUM/3/nummod गेंद/NOUN/6/nmod की/ADP/3/case अपनी/PRON/6/nmod पारी/NOUN/13/obl में/ADP/6/case चार/NUM/9/nummod चौके/NOUN/13/obj और/CCONJ/12/cc एक/NUM/12/nummod छक्का/NOUN/9/conj मारा/VERB/0/root ।/PUNCT/13/punct",
"इनके/PRON/2/obl लिखे/VERB/7/obl में/ADP/2/case कोई/DET/5/det बनावट/NOUN/7/nsubj नहीं/PART/7/advmod होती/VERB/0/root ।/PUNCT/7/punct",
"इन/DET/2/det लोगों/NOUN/4/nmod का/ADP/2/case व्यवहार/NOUN/7/obl से/ADP/4/case खास/ADJ/7/amod लेना/NOUN/11/nsubj -/PUNCT/9/punct देना/NOUN/7/conj नहीं/PART/11/advmod होता/VERB/0/root ।/PUNCT/11/punct",
"एक/NUM/2/nummod बार/NOUN/12/obl फिर/ADV/12/advmod से/ADP/3/case बंगाली/ADJ/6/amod बाला/NOUN/8/nmod का/ADP/6/case जादू/NOUN/12/nsubj बॉक्स/NOUN/10/compound ऑफिस/NOUN/12/obl पर/ADP/10/case चल/VERB/0/root गया/AUX/12/aux ।/PUNCT/12/punct",
]
# Sentences we wrote ourselves, one per line in the same form (selfdev_own.txt next to this script).
import os
OWN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "selfdev_own.txt")
S += [l.strip() for l in open(OWN, encoding="utf-8") if l.strip()]
NOSPACE_BEFORE = {".", "।", ",", "”", "?"}
out = []
for n, line in enumerate(S, 1):
    toks = [t.rsplit("/", 3) for t in line.split(" ")]
    assert all(len(t) == 4 for t in toks), line
    for t in toks:
        h = int(t[2]); assert 0 <= h <= len(toks), line
    assert sum(1 for t in toks if t[2] == "0") == 1, line
    for i in range(len(toks)):  # every token reaches the root without a cycle
        j, steps = i + 1, 0
        while j != 0:
            j = int(toks[j - 1][2]); steps += 1
            assert steps <= len(toks), ("cycle", line)
    text = ""
    rows = []
    for i, (f, u, h, r) in enumerate(toks):
        nxt = toks[i + 1][0] if i + 1 < len(toks) else None
        nospace = nxt is not None and (nxt in NOSPACE_BEFORE or (f == "'" and i == 6 and n == 5))
        if n == 5 and nxt == "'" and i == 8:
            nospace = True
        text += f + ("" if nospace or nxt is None else " ")
        rows.append((i + 1, f, u, h, r, "SpaceAfter=No" if nospace else "_"))
    out.append(f"# sent_id = hi-selfdev-{n}\n# text = {text}")
    for i, f, u, h, r, m in rows:
        out.append(f"{i}\t{f}\t_\t{u}\t_\t_\t{h}\t{r}\t_\t{m}")
    out.append("")
open(sys.argv[1], "w", encoding="utf-8").write("\n".join(out) + "\n")
print(len(S), "sentences", sum(len(l.split(" ")) for l in S), "tokens")
