
'''
=== Raw AST ===
RawAst(
    [
        Load {
            resource: "transcript",
            path: "meeting.txt",
        },
        Extract {
            target: "action items",
            input: Some(
                Resource(
                    "transcript",
                ),
            ),
        },
        Summarize {
            input: Resource(
                "action items",
            ),
            prompt: None,
        },
        Rewrite {
            input: Resource(
                "summary",
            ),
            prompt: Some(
                "Rewrite in a friendly, conversational tone suitable for a Slack update.",
            ),
        },
    ],
)

=== Normalized AST ===
NormalizedAst(
    [
        Load {
            resource: "transcript",
            path: "meeting.txt",
        },
        Extract {
            target: "action items",
            input: Resource(
                "transcript",
            ),
        },
        Summarize {
            input: Resource(
                "action items",
            ),
            prompt: None,
        },
        Rewrite {
            input: Resource(
                "summary",
            ),
            prompt: Some(
                "Rewrite in a friendly, conversational tone suitable for a Slack update.",
            ),
        },
    ],
)

=== IR ===
[
  [0] Load { path: "meeting.txt" },
  [1] Extract { target: "action items", input: $0 },
  [2] Summarize { input: $1, prompt: None },
  [3] Rewrite { input: $2, prompt: Some("Rewrite in a friendly, conversational tone suitable for a Slack update.") }
]

=== Evaluation ===

--- Operation [0]: Load { path: "meeting.txt" } ---
Loaded 4531 bytes from 'meeting.txt'

--- Operation [1]: Extract { target: "action items", input: $0 } ---
Prompt:
Extract the action items from the following text:

Project Phoenix – Weekly Sync
Date: June 24, 2026
Attendees: Don, Priya, Marcus, Elena**

Transcript:
Don: Alright folks, thanks for hopping on. I know everyone’s juggling a lot this week, so let’s try to keep this focused… but also, you know, not soul‑crushingly serious.

Priya: Love that energy. I brought coffee, metaphorically.

Marcus: I brought actual coffee, and it’s already not working.

Elena: I brought a cat who refuses to leave my keyboard, so if I suddenly type “mmmmmmmmm” that’s him.

Don: Perfect. Let’s start with the onboarding flow redesign. We’re still seeing drop‑offs on step three. We need a concrete plan before the July milestone. I know we talked about this last week, but the numbers haven’t budged.

Priya: Yeah, I dug into the analytics again yesterday. The permissions screen is still the biggest friction point. The copy feels too technical — people hesitate because they think we’re asking for more than we actually are.

Marcus: I mean, to be fair, the current copy reads like it was written by a committee of lawyers who were all having a bad day.

Elena: Accurate. I can take a pass at rewriting it, but I want UX to validate the tone. I’ll mock up two alternative versions by Friday and share them.

Priya: Great — I’ll rewrite the copy itself, but I’ll wait for your mockups so the tone matches the visuals.

Don: Awesome. And Marcus, you mentioned last week that we should update the event tracking?

Marcus: Yep. I’ll update the instrumentation so we can measure whether the new copy actually improves conversion. Should have that ready early next week. Assuming my cat doesn’t delete my code.

Elena: He only deletes the important parts.

Don: Okay, second topic: the customer advisory board session next month. We need a draft agenda and a short slide deck outlining the roadmap themes.

Priya: I can own the agenda draft. I’ll have a first version by Tuesday. And yes, I promise it won’t be 14 pages long like last time.

Marcus: I’ll start the slide deck. I’m thinking six to eight slides, clean visuals, no gradients that look like 2007 PowerPoint themes.

Elena: Hey, 2007 was a vibe.

Don: A vibe we’re not bringing back. Marcus, Wednesday for the draft?

Marcus: Yep, Wednesday works.

Elena: I’ll review both the agenda and the deck once they’re ready — mostly for consistency, but also to make sure we’re not accidentally promising to deliver a spaceship by Q3.

Priya: Honestly, that might impress the advisory board.

Don: Third topic: the accessibility audit vendor. We’ve got two quotes. I’ll compare them side‑by‑side and send a recommendation by end of day tomorrow.

Marcus: Do we know if either vendor can start before July 10th?

Don: Vendor A can. Vendor B said maybe, depending on staffing. I’ll include that in the comparison.

Elena: I still think Vendor A’s communication style is better, but I’ll wait for your breakdown.

Don: Cool. Now, before we move on — anything else on onboarding? Any blockers?

Priya: Not a blocker, but I’d love to run a quick user test on the new copy once Elena’s mockups are ready. Just a small sample — maybe five users.

Marcus: I can help recruit. We still have that pool from last quarter.

Don: Great. Let’s do that. Not an official action item yet, but let’s keep it in mind.

Elena: Speaking of keeping things in mind… can we talk about the color palette update? Not an action item, just a heads‑up. I’m exploring a slightly warmer tone for the primary accent color.

Marcus: As long as it’s not “sunset orange.” That color haunts me.

Priya: I actually liked sunset orange.

Marcus: I stand by my statement.

Don: Okay, color therapy session aside, anything else?

Priya: Oh — small thing. The support team asked if we could clarify the difference between “workspace” and “project” in the UI. People keep mixing them up.

Elena: Yeah, that’s on my radar. Not an action item yet, but I’ll bring a proposal next week.

Don: Perfect. And last thing: next week’s sync might need to move. I’ve got a conflict Wednesday morning.

Marcus: I’m flexible.

Priya: Same.

Elena: As long as it’s not before 9am. My brain doesn’t exist before 9am.

Don: Noted. I’ll send a poll.

Priya: Look at us, being all collaborative.

Marcus: It’s the coffee.

Don: Alright, that’s everything. Thanks everyone — good energy today.

Model output:
Here are the action items extracted from the meeting transcript:

**Elena**
- Mock up two alternative versions of the permissions screen copy by Friday
- Review the advisory board agenda and slide deck for consistency once they're ready
- Bring a proposal next week regarding clarifying "workspace" vs. "project" in the UI

**Priya**
- Rewrite the permissions screen copy after Elena's mockups are ready
- Draft the customer advisory board agenda by Tuesday

**Marcus**
- Update event tracking instrumentation to measure conversion impact of new copy, ready early next week
- Start the customer advisory board slide deck draft, due Wednesday

**Don**
- Compare the two accessibility audit vendor quotes side-by-side and send a recommendation by end of day tomorrow (June 25)
- Send a scheduling poll to move next week's sync

**Pending / Not Yet Official**
- Priya to run a quick user test (~5 users) on the new permissions copy once Elena's mockups are ready
- Marcus to help recruit participants from last quarter's user pool for the above test

--- Operation [2]: Summarize { input: $1, prompt: None } ---
Prompt:
Summarize the following text clearly and concisely:

Here are the action items extracted from the meeting transcript:

**Elena**
- Mock up two alternative versions of the permissions screen copy by Friday
- Review the advisory board agenda and slide deck for consistency once they're ready
- Bring a proposal next week regarding clarifying "workspace" vs. "project" in the UI

**Priya**
- Rewrite the permissions screen copy after Elena's mockups are ready
- Draft the customer advisory board agenda by Tuesday

**Marcus**
- Update event tracking instrumentation to measure conversion impact of new copy, ready early next week
- Start the customer advisory board slide deck draft, due Wednesday

**Don**
- Compare the two accessibility audit vendor quotes side-by-side and send a recommendation by end of day tomorrow (June 25)
- Send a scheduling poll to move next week's sync

**Pending / Not Yet Official**
- Priya to run a quick user test (~5 users) on the new permissions copy once Elena's mockups are ready
- Marcus to help recruit participants from last quarter's user pool for the above test

Model output:
## Meeting Action Items Summary

The team has several tasks due over the next week:

- **Elena** will create two mockup versions of the permissions screen copy by Friday and will later review the advisory board materials for consistency. She'll also bring a proposal next week on clarifying "workspace" vs. "project" terminology in the UI.

- **Priya** will rewrite the permissions screen copy once Elena's mockups are complete and will draft the customer advisory board agenda by Tuesday.

- **Marcus** will update event tracking to measure the impact of the new copy (ready early next week) and begin the advisory board slide deck draft, due Wednesday.

- **Don** will compare accessibility audit vendor quotes and send a recommendation by end of day June 25, and will send a poll to reschedule next week's sync.

**Pending items** include a quick user test (~5 users) on the new permissions copy, with Priya leading and Marcus helping recruit participants from last quarter's user pool — both contingent on Elena's mockups being finalized.

--- Operation [3]: Rewrite { input: $2, prompt: Some("Rewrite in a friendly, conversational tone suitable for a Slack update.") } ---
Prompt:
Rewrite in a friendly, conversational tone suitable for a Slack update.

Input:
## Meeting Action Items Summary

The team has several tasks due over the next week:

- **Elena** will create two mockup versions of the permissions screen copy by Friday and will later review the advisory board materials for consistency. She'll also bring a proposal next week on clarifying "workspace" vs. "project" terminology in the UI.

- **Priya** will rewrite the permissions screen copy once Elena's mockups are complete and will draft the customer advisory board agenda by Tuesday.

- **Marcus** will update event tracking to measure the impact of the new copy (ready early next week) and begin the advisory board slide deck draft, due Wednesday.

- **Don** will compare accessibility audit vendor quotes and send a recommendation by end of day June 25, and will send a poll to reschedule next week's sync.

**Pending items** include a quick user test (~5 users) on the new permissions copy, with Priya leading and Marcus helping recruit participants from last quarter's user pool — both contingent on Elena's mockups being finalized.

Model output:
Hey team! 👋 Here's a quick recap of what we've all got on our plates after today's meeting:

**Elena** is kicking things off by creating two mockup versions of the permissions screen copy by Friday. After that, she'll review the advisory board materials for consistency, and she's also going to bring a proposal next week on clearing up the whole "workspace" vs. "project" terminology situation in the UI.

**Priya** will jump into rewriting the permissions screen copy once Elena's mockups are ready, and she's also drafting the customer advisory board agenda — that one's due Tuesday.

**Marcus** is updating event tracking so we can measure how the new copy performs (should be ready early next week), plus he's starting on the advisory board slide deck, due Wednesday.

**Don** is comparing accessibility audit vendor quotes and will send a recommendation by EOD June 25. He'll also be sending around a poll to find a better time for next week's sync — keep an eye out for that!

⏳ **A couple of things still in the works:** Priya will lead a quick user test on the new permissions copy (around 5 users), with Marcus helping recruit from last quarter's user pool. Both of those are waiting on Elena's mockups to be finalized first.

Let us know if anything looks off or if you have questions. Thanks everyone! 🙌

=== Final Result ===
Hey team! 👋 Here's a quick recap of what we've all got on our plates after today's meeting:

**Elena** is kicking things off by creating two mockup versions of the permissions screen copy by Friday. After that, she'll review the advisory board materials for consistency, and she's also going to bring a proposal next week on clearing up the whole "workspace" vs. "project" terminology situation in the UI.

**Priya** will jump into rewriting the permissions screen copy once Elena's mockups are ready, and she's also drafting the customer advisory board agenda — that one's due Tuesday.

**Marcus** is updating event tracking so we can measure how the new copy performs (should be ready early next week), plus he's starting on the advisory board slide deck, due Wednesday.

**Don** is comparing accessibility audit vendor quotes and will send a recommendation by EOD June 25. He'll also be sending around a poll to find a better time for next week's sync — keep an eye out for that!

⏳ **A couple of things still in the works:** Priya will lead a quick user test on the new permissions copy (around 5 users), with Marcus helping recruit from last quarter's user pool. Both of those are waiting on Elena's mockups to be finalized first.

Let us know if anything looks off or if you have questions. Thanks everyone! 🙌