# Culture redesign: implementation and closeout

The three waves are integrated in the working tree; T10 adds the cultural atlas and calendar-hosted performances. See [FINAL_REPORT.md](FINAL_REPORT.md) for fixes and validation and [T10_REPORT.md](T10_REPORT.md) for implementation details. The dispatch order below records the original plan.

Read [the full audit and research](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CULTURE_REDESIGN.md>) and [integration notes](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/INTEGRATION.md>) first.

## Dispatch order

1. Stabilize and preserve the current working tree, including the recipe agent's handoff and untracked culture files. Freeze the common contracts. Use an environment that offers Opus; the planning session does not expose that model.
2. Wave 1: Agent A runs T01, B runs T02, C runs T03. Integrate and pass the origin/locality/persistence gate.
3. Wave 2: Agent A runs T04, B runs T05, C runs T06. Integrate and pass the observance/serving/teaching gate.
4. Wave 3: Agent A runs T07, B runs T08, C runs T09 followed by T10. Integrate and pass all three end-to-end scenarios and the release checks.

Three agents by three waves is nine parallel slots. Agent C's final slot contains two sequential, bounded tasks; this is not ten simultaneous jobs. Agent A owns shared integration and final build verification, while each agent fixes defects in its own files. No task can claim success merely because its branch passes without the other modules.

## Briefs
- [T01 — Living traditions with visible causes](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T01.md>)
- [T02 — Local cultures and routes of exchange](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T02.md>)
- [T03 — Shared wounds, memorial places, and inheritance](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T03.md>)
- [T04 — A meaningful calendar and ritual repertoire](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T04.md>)
- [T05 — Hospitality, redistribution, and access to culture](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T05.md>)
- [T06 — Apprenticeships and cultural institutions](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T06.md>)
- [T07 — Earned syncretism and specific ruin inheritance](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T07.md>)
- [T08 — Ensembles whose relationships affect their performance](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T08.md>)
- [T09 — Public memory and civic legitimacy](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T09.md>)
- [T10 — A cultural atlas that explains and compares choices](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/T10.md>)

## Recipe owner boundary

The recipe agent owns composition, derivation, validation, runtime resource registration and recipe editor behavior. The culture additions consume stable recipe references and committed serving/teaching/dedication events. Do not fork the recipe algorithm, turn ingredient use into meal attendance, or assume generic food-value spending served a selected named recipe.

## Integration responsibilities

Agent A integrates core CultureSystem/CultureState/Settings, shared assets, save lifecycle, condition/effect/content registration, and the final build. Agent B owns contact/performance world adapters. Agent C owns evidence/accounts and final culture UI composition. Feature tasks contribute separate panel classes; they do not all edit CultureWindow.cs.

The main design is the authoritative specification. These briefs are dispatch copies; update a brief if its task section changes before execution.