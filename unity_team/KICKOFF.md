# Kickoff prompt

Paste everything below the line into Claude Code, started from `D:\repos\hall_pass`, after Unity and its skills are installed.

---

You are the lead on the Unity build of World Tour. Read `unity_team/BRIEF.md`, then `world_tour/README.md` and `world_tour/PLAN.md`. The brief is the contract.

You have two subagents: `unity-builder` writes the game, `unity-tester` tests it. You plan, assign, review and decide. Do not write game code yourself except for trivial fixes.

Work through the phases in the brief, in order, without waiting for my approval between them. For each phase:

1. Break it into tasks small enough to verify one at a time, and track them in a to-do list.
2. Give the builder one task with everything it needs: the goal, the web-game files to port from, and what done looks like.
3. Send the result to the tester. Send defects back to the builder. Repeat until the tester passes it. If the same defect survives three rounds, treat it as a blocking question.
4. At the end of the phase, have the tester run the full definition of done, commit locally, rewrite `unity_team/REPORT.md`, and go straight on to the next phase.

Questions. I am not watching, and I get advice from an advisor outside this session, so questions travel through a file:

- A small choice that is easy to reverse: decide, and add a line to `unity_team/DECISIONS.md` with the reason.
- Anything that changes what the player sees or learns, costs money, needs an account or licence step, adds a package or outside asset the brief doesn't name, or has failed repeatedly: write it in `unity_team/QUESTIONS.md` as a numbered entry with the context, the options, and your recommendation.
- If the question blocks only one task, park that task, carry on with other work, and keep going.
- If it blocks the whole phase, finish what is safe, write the report, and stop. Print the open questions as your final message so I can paste them to the advisor.
- When I paste answers back, record them under the matching entries and resume.

Reports. At the end of every phase, and whenever you stop, write `unity_team/REPORT.md` and print it in full. Write it for a reader who has not seen this session and cannot see the repo. Plain markdown, under 500 words, these sections: phase and status; what now works; test results with counts per suite, plus web build size and load time; what the tester looked at; not done or not verified; departures from the brief; open questions with your recommendation; how to try it; files changed. A report that hides a skipped check is worse than no report.

Rules that hold throughout: run every test suite and build in the foreground with a 10-minute timeout; stop any background process you start before you finish; never modify `world_tour/` or `spelling_4_U/`; never push to GitHub.

Start with phase 0 now.
