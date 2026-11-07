#!/usr/bin/env python3
"""Surveys every Universal Dependencies treebank at one release tag: licence lines, splits, file names and split sizes.

usage: ud_survey.py <work-dir> [--tag r2.18]

For every repository `UniversalDependencies/UD_<Language>-<Treebank>` it reads, at the tag, the README (the
`License:` metadata line and the lines that mention restricted text sources), `LICENSE.txt` (first lines) and
`stats.xml` (sentence and token counts per split), then finds the CoNLL-U file names by probing
`<code>_<treebank>-ud-{train,dev,test}.conllu`. 
Writes <work-dir>/survey.tsv (one row per treebank) and <work-dir>/flags.txt (README lines worth a human
read: permissions, research-only wording, tweets, Reddit, LDC, Penn). Raw pages are cached under
<work-dir>/cache so a re-run only fetches what is missing. Only needs python3. The verdicts that come out of
this survey are in docs/ud-licences.md; this script reports facts, it does not decide.
"""
import concurrent.futures as cf
import json
import os
import re
import sys
import urllib.error
import urllib.request

ORG = "UniversalDependencies"
RAW = "https://raw.githubusercontent.com/" + ORG + "/{repo}/{tag}/{file}"
API = "https://api.github.com/orgs/" + ORG + "/repos?per_page=100&page={page}"
CODES = "https://raw.githubusercontent.com/" + ORG + "/docs-automation/master/codes_and_flags.yaml"
SPLITS = ("train", "dev", "test")
FLAG_WORDS = re.compile(
    r"permission|academic|research|non-?commercial|twitter|tweet|reddit|LDC|Linguistic Data|Penn|proprietary|"
    r"copyright|restrict|terms of (use|service)|scrap|crawl|license[d]? (from|by|under)|all rights", re.I)


def fetch(url, cache, binary=False, missing_ok=True):
    """Returns the body (cached on disk) or None when the server says 404."""
    if os.path.exists(cache):
        with open(cache, "rb") as f:
            data = f.read()
        return None if data == b"\0404" else (data if binary else data.decode("utf-8", "replace"))
    os.makedirs(os.path.dirname(cache), exist_ok=True)
    for attempt in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "onenlp-survey"}), timeout=120) as r:
                data = r.read()
            with open(cache, "wb") as f:
                f.write(data)
            return data if binary else data.decode("utf-8", "replace")
        except urllib.error.HTTPError as e:
            if e.code == 404 and missing_ok:
                with open(cache, "wb") as f:
                    f.write(b"\0404")
                return None
            last = e
        except Exception as e:  # noqa: BLE001 - retry
            last = e
    raise SystemExit(f"download failed: {url}: {last}")


def exists(url, cache):
    """HEAD probe, cached; avoids downloading the data just to learn a file name."""
    if os.path.exists(cache):
        return open(cache).read() == "1"
    os.makedirs(os.path.dirname(cache), exist_ok=True)
    ok = None
    for attempt in range(4):
        try:
            urllib.request.urlopen(urllib.request.Request(url, method="HEAD", headers={"User-Agent": "onenlp-survey"}), timeout=60)
            ok = True
            break
        except urllib.error.HTTPError as e:
            if e.code == 404:
                ok = False
                break
        except Exception:  # noqa: BLE001 - retry
            pass
    if ok is None:
        raise SystemExit(f"probe failed: {url}")
    with open(cache, "w") as f:
        f.write("1" if ok else "0")
    return ok


def repos(work):
    cache = os.path.join(work, "cache", "repos.json")
    if not os.path.exists(cache):
        names = []
        for page in range(1, 8):
            body = fetch(API.format(page=page), os.path.join(work, "cache", f"repos-{page}.json"))
            items = json.loads(body)
            if not items:
                break
            names += [r["name"] for r in items]
        with open(cache, "w") as f:
            json.dump(sorted(n for n in names if n.startswith("UD_")), f)
    with open(cache) as f:
        return json.load(f)


def language_codes(work):
    """Language name (underscored, as in repository names) -> list of candidate ISO codes."""
    text = fetch(CODES, os.path.join(work, "cache", "codes.yaml"))
    out, name = {}, None
    for line in text.splitlines():
        m = re.match(r"^(\S.*?):\s*$", line)
        if m:
            name = m.group(1).strip("'\"").replace(" ", "_")
            out[name] = []
            continue
        m = re.match(r"^\s+(lcode|iso3):\s*(\S+)", line)
        code = m.group(2).strip("'\"") if m else ""
        if m and name and code not in out[name]:
            out[name].append(code)
    return out


def license_line(readme):
    m = re.search(r"^\s*(?:\*\s*)?License:\s*(.+?)\s*$", readme or "", re.M)
    return m.group(1) if m else ""


def stats(xml):
    out = {}
    if not xml:
        return out
    for s in SPLITS:
        m = re.search(r"<%s><sentences>(\d+)</sentences><tokens>(\d+)</tokens>" % s, xml)
        if m:
            if int(m.group(1)) > 0:  # test-only treebanks list empty train and dev sizes
                out[s] = (int(m.group(1)), int(m.group(2)))
    return out


def survey_one(repo, tag, work, codes):
    lang, _, tb = repo[3:].partition("-")
    c = os.path.join(work, "cache", tag, repo)
    readme = fetch(RAW.format(repo=repo, tag=tag, file="README.md"), c + "/README.md") or \
        fetch(RAW.format(repo=repo, tag=tag, file="README.txt"), c + "/README.txt")
    lic = fetch(RAW.format(repo=repo, tag=tag, file="LICENSE.txt"), c + "/LICENSE.txt")
    xml = fetch(RAW.format(repo=repo, tag=tag, file="stats.xml"), c + "/stats.xml")
    st = stats(xml)
    prefix, files = "", {}
    if readme is not None:
        slug = tb.lower().replace("-", "_") if tb else ""
        for code in codes.get(lang, []):
            cand = f"{code}_{slug}"
            for s in SPLITS:
                if s in st:
                    url = RAW.format(repo=repo, tag=tag, file=f"{cand}-ud-{s}.conllu")
                    marker = c + f"/probe-{cand}-{s}"
                    if exists(url, marker):
                        files[s] = url
            if files:
                prefix = cand
                break
    lic_head = " ".join((lic or "").split()[:14])
    flags = []
    if readme:
        for ln in readme.splitlines():
            if FLAG_WORDS.search(ln) and len(ln) < 600:
                flags.append(ln.strip())
    return dict(repo=repo, lang=lang, tb=tb, has_readme=readme is not None, readme_licence=license_line(readme),
                licence_file=lic_head, stats=st, prefix=prefix, files=files, flags=flags[:12])


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    tag = "r2.18"
    if "--tag" in sys.argv:
        tag = sys.argv[sys.argv.index("--tag") + 1]
        args = [a for a in args if a != tag]
    if not args:
        raise SystemExit(__doc__)
    work = args[0]
    os.makedirs(work, exist_ok=True)
    names, codes = repos(work), language_codes(work)
    with cf.ThreadPoolExecutor(8) as ex:
        rows = list(ex.map(lambda r: survey_one(r, tag, work, codes), names))
    with open(os.path.join(work, "survey.tsv"), "w", encoding="utf-8") as f:
        f.write("repo\tlanguage\ttreebank\tin_release\treadme_licence\tlicence_file_head\tprefix\tsplits\tsentences\ttokens\n")
        for r in rows:
            sp = ",".join(s for s in SPLITS if s in r["files"])
            sent = ",".join(str(r["stats"].get(s, ("", ""))[0]) for s in SPLITS)
            tok = ",".join(str(r["stats"].get(s, ("", ""))[1]) for s in SPLITS)
            f.write("\t".join([r["repo"], r["lang"], r["tb"], "yes" if r["has_readme"] else "no", r["readme_licence"],
                               r["licence_file"], r["prefix"], sp, sent, tok]) + "\n")
    with open(os.path.join(work, "flags.txt"), "w", encoding="utf-8") as f:
        for r in rows:
            if r["flags"]:
                f.write("## " + r["repo"] + "\n" + "\n".join("  " + x for x in r["flags"]) + "\n")
    print(f"{len(rows)} repositories, {sum(1 for r in rows if r['has_readme'])} in {tag}")


if __name__ == "__main__":
    main()
