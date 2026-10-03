# EagleEye — Manual Testing

*Status: Active (introduced 2026-10-03, see ADR-007)*
*Owner: TES agent | Executed by: Michael*

All acceptance and end-to-end testing for EagleEye is **manual**. Michael executes every test. The TES agent prepares the test plans, guides the test session, and evaluates the results. There are no automated E2E tests.

---

## 1. Roles

| Who | Does |
|---|---|
| **TES** | Derives test cases from the user story's acceptance criteria (black box), writes the test plan and the test-run checklist, writes setup instructions, evaluates the recorded results, writes the test report and the issues |
| **Michael** | Reviews the test plan, executes the checklist, records results and observations, approves the test report |
| **DEV** | Before handing over, checks that the build installs and starts on the Windows machine (smoke check). Fixes issues. |

---

## 2. Test Machines

| Machine | Tested there |
|---|---|
| **Windows Developer Machine** (main test station) | Installer, `EagleEye.Service`, `EagleEye.TrayClient`, Windows parent app, Android parent app (emulator or USB device) |
| **MacBook** | macOS parent app only. It connects over the LAN to the service on the Windows machine. |

### Test accounts on the Windows machine

- **Michael's admin account**: used to install EagleEye and for the parent apps. Admin accounts are never monitored, so EagleEye does not interfere with development work.
- **A local standard (non-admin) test account** (e.g. `eagleeye-kid`): the "kid". Use *Switch user* to move between the two accounts without logging off.

> Caution: the service enforces rules for every standard account on the machine. Do not use a standard account for anything you care about while EagleEye is installed.

---

## 3. Workflow per User Story

```
1. TES writes  docs/testing/US-XXX/test-plan.md           → Michael reviews/approves
2. TES creates docs/testing/US-XXX/test-run-01.md         (checklist, from template)
3. Michael executes the checklist and fills in results     (Pass/Fail/Blocked/Skipped + notes)
4. Michael tells TES: "test run 01 for US-XXX is done"
5. TES evaluates → writes docs/testing/US-XXX/test-report.md
                 → writes issues in docs/requirements/user-stories/US-XXX/issues/ISSUE-XXX.md
6. After fixes: TES creates test-run-02.md (failed cases + regression) → repeat 3–5
7. Michael approves the test report → story can be closed (by Michael only)
```

Every test run includes the **regression checklist** (`docs/testing/regression-checklist.md`), which covers all previously closed user stories. TES adds a story's key cases to that checklist when the story is closed.

---

## 4. Recording Results (Feedback Mechanism)

Michael records results **directly in the test-run file** in VSCode. This works on either machine. Each test case has this block:

```markdown
- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:
```

- Tick exactly one box (`[x]`).
- **Observed**: required for Fail or Blocked. What actually happened.
- **Notes**: optional. Remarks, ideas, or "unclear what to expect here". TES forwards these to PRO as requirement feedback.
- **Evidence**: put screenshots and log excerpts in `docs/testing/US-XXX/evidence/` and reference them by file name (e.g. `evidence/tc-03-tray-red.png`).
- **Free-form feedback**: anything that does not fit a test case goes in the "General Feedback" section at the end of the run file. Michael can also just tell TES in chat; TES then writes it into the run file so it is not lost.

Partial runs are fine. Unticked cases count as "not executed".

### Where to find logs for evidence

| Component | Location (Windows) |
|---|---|
| Service, TrayClient | `%ProgramData%\EagleEye\` |
| Windows Event Log (pairing code fallback) | Event Viewer → Windows Logs → Application, source `EagleEye` |
| macOS parent app | `~/Library/Application Support/EagleEye/` |

---

## 5. Files and Naming

```
docs/testing/
├── README.md                       ← this file
├── regression-checklist.md         ← cumulative regression cases for all closed stories
├── templates/
│   ├── test-plan-template.md
│   ├── test-run-template.md
│   └── test-report-template.md
└── US-XXX/
    ├── test-plan.md                ← TES, approved by Michael
    ├── test-run-01.md, -02.md …    ← executed checklists; never overwritten
    ├── test-report.md              ← TES evaluation, approved by Michael
    └── evidence/                   ← screenshots, log excerpts
```

- Test case IDs: `TC-<story>-<nn>` (e.g. `TC-001-07`). Each test case references the AC(s) it verifies.
- Test runs are permanent records. A new run gets a new file.
- Issues stay with their story: `docs/requirements/user-stories/US-XXX/issues/ISSUE-XXX.md`.
