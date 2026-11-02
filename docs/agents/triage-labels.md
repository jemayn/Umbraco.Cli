# Triage Labels

The skills speak in terms of five canonical triage roles. This repo uses the prefixed label style of other Umbraco repositories (`type/*`, `state/*`, `community/*`), so each role maps to a `state/*` label.

| Role              | Label                    | Meaning                                  |
| ----------------- | ------------------------ | ---------------------------------------- |
| `needs-triage`    | `state/needs-triage`     | Maintainer needs to evaluate this issue  |
| `needs-info`      | `state/needs-more-info`  | Waiting on reporter for more information |
| `ready-for-agent` | `state/ready-for-agent`  | Fully specified, ready for an AFK agent  |
| `ready-for-human` | `state/ready-for-human`  | Requires human implementation            |
| `wontfix`         | `state/wontfix`          | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label from this table.

The issue forms in `.github/ISSUE_TEMPLATE` apply `state/needs-triage` together with `type/bug` or `type/feature`; `.github/release.yml` groups release notes by the `type/*` labels.
