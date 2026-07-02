# Public Release Checklist — Limelight‑X

This checklist ensures that Limelight‑X is polished, consistent, and ready for public visibility.  
Complete each section before changing the repository visibility to **Public**.

---

## 1. Required Public‑Facing Files

- [ ] `README.md` is complete, accurate, and polished  
- [ ] `LICENSE` (MIT) is present in the root  
- [ ] `CODE_OF_CONDUCT.md` added and reviewed  
- [ ] `CONTRIBUTING.md` added and reviewed  
- [ ] `SECURITY.md` added and reviewed  
- [ ] `SUPPORT.md` added and reviewed  
- [ ] `CITATION.cff` added and validated  
- [ ] Issue templates added (`bug_report`, `feature_request`, `dsl_proposal`)  
- [ ] Pull request template added  
- [ ] `.github` folder structure is correct and clean  

---

## 2. Repository Hygiene

- [ ] No secrets or tokens in commit history  
- [ ] No large accidental binaries committed  
- [ ] No temporary files, scratch notes, or experimental branches  
- [ ] No dead code or unused directories  
- [ ] All examples run as written  
- [ ] CLI help text is correct and up to date, including `llx serve`  
- [ ] Grammar, IR, evaluator, and normalization docs are consistent  
- [ ] `/src/api` (`llx serve`) and `/ui` docs (`api.md`, `spec/ux/*.md`) are consistent with each other  
- [ ] Commit history is clean and readable  

---

## 3. Branch Protection & Settings

- [ ] `main` branch is protected  
  - [ ] PRs required before merge  
  - [ ] At least one review required  
  - [ ] Status checks required (tests, lint, build)  
  - [ ] Force‑push disabled  
  - [ ] Branch deletion disabled  

- [ ] Only intended collaborators have write access  
- [ ] No leftover temporary collaborators  

---

## 4. GitHub Security & Analysis

Enable all recommended GitHub security features:

- [ ] Dependabot alerts  
- [ ] Dependabot security updates  
- [ ] Secret scanning  
- [ ] Secret scanning push protection  
- [ ] CodeQL code scanning  

---

## 5. Versioning & Release Prep

- [ ] Version number set (e.g., `0.1.0`)  
- [ ] `CHANGELOG.md` created or updated  
- [ ] First release tag created:  
  ```
  git tag -a v0.1.0 -m "Initial public release"
  git push --tags
  ```

---

## 6. Documentation & Positioning

- [ ] Project tagline is clear and concise  
- [ ] Purpose and scope are well defined  
- [ ] DSL examples are correct and minimal  
- [ ] IR examples are correct and readable  
- [ ] Evaluator semantics documented  
- [ ] Normalization rules documented  
- [ ] `/src/api` HTTP examples (`POST /run`, `/explain`, `/trace`) documented  
- [ ] Roadmap or “Future Work” section included  

---

## 7. Optional but Recommended

- [ ] `CODEOWNERS` file added  
- [ ] GitHub Discussions enabled (if desired)  
- [ ] GitHub Pages configured or explicitly disabled  
- [ ] Screenshots, diagrams, or architecture overview added — including `/ui` screenshots if the UI is included in this release  
- [ ] “Good First Issues” labeled for new contributors  
- [ ] If `/ui` is part of this release: MSIX installer build verified, and `ANTHROPIC_API_KEY` setup steps documented for end users (see `spec/ux/ui-deployment.md`)  

---

## 8. Final Pre‑Public Verification

- [ ] View the repo in a private browser window to confirm:  
  - [ ] README renders correctly  
  - [ ] All badges work  
  - [ ] All links work  
  - [ ] Community Standards page shows green checks  
  - [ ] Security tab shows no critical alerts  

---

## 9. Flip Visibility

Once everything above is complete:

**Settings → General → Change visibility → Public**

After flipping:

- [ ] Verify repo loads correctly as a public user  
- [ ] Verify Actions logs are appropriate for public visibility  
- [ ] Verify Discussions, Issues, and PR templates work  
- [ ] Verify `CITATION.cff` renders the “Cite this repository” button  

---

## Done

Limelight‑X is now publicly available and ready for contributors, researchers, and curious developers.

