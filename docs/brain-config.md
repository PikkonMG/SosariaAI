# brain.json reference

`brain.json` sets the model providers, the routes that send calls to them, and
the limits and budgets on those calls. It lives here:

```text
ModernUO/Distribution/Configuration/sosariaai/brain.json
```

First boot writes the file when it is missing, with `frontier`, `local`, `jev`,
`von`, and `laya` provider entries. Later boots keep your edits and do not add
new keys, so a key that is not in the file gets its default. A file made before
a provider was added does not get that provider: copy its entry from
[Scenarios](#scenarios). The
server reads the file only at boot, so restart it after each edit. If an edit
breaks the JSON, the log names the file and the line, and the whole file falls
back to its defaults.

The game rules are always the fallback. When a provider is off, slow, paused,
over budget, or unsure, the character uses its own rules.

## Two kinds of provider

| Kind | `api` value | What it does | Examples |
|---|---|---|---|
| Chat | `"openai"` or `null` | Writes text over an OpenAI-style chat completions API: speech, musing, plans, personas, trade rewording, and decisions. | OpenRouter, Ollama, llama.cpp, any OpenAI-compatible endpoint |
| System One | `"systemone"` | Answers typed questions with a pick and a confidence. It writes no text and serves decision calls only. | Jev, Von, Laya |

## Two routes

Every call is one of two kinds, and `route` names the provider for each kind.

- `route.chat` carries speech replies, musing, persona writes, and trade
  rewording. It must name a chat provider. A System One provider on this route
  does nothing.
- `route.decision` carries next jobs, big moments, player intent, fight
  stances, trade reads, the speech gate, and multi-step plans.
  - With a chat provider here, the model writes plans and picks routines.
  - With a System One provider here, the model picks jobs and stances but
    writes no plans, so the rules make the plans.
- `route.decisionUses` is optional. It sends one decision use to a different
  provider. A use that is not listed follows `route.decision`.

| `decisionUses` key | What it sends |
|---|---|
| `decision` | A routine next job. Sent only when `jevScope` is `"all"`. |
| `bigMoment` | A next job or event decision at a big moment, a player's own words (intent), and the red moments (see [Red moments](#red-moments)). |
| `personFight` | A fight stance against a person. |
| `monsterFight` | A fight stance against a monster. |
| `trade` | A player's haggle line that fits no trade rule. |
| `speechGate` | One character hears another: is the line worth a reply? |

The speech gate, player intent, fight stances, trade reads, and red moments
need a System One provider. When their use routes to a chat provider, the
rules decide.

## What each System One model decides

The plugin works out which model a System One provider runs and sends that
model only the tasks it does well. The rules take every other task for free,
so a weak guess never moves a character.

| Task | Jev | Von | Laya |
|---|---|---|---|
| Fight stances | yes | yes | yes |
| Next jobs and big moments | yes | rules | rules |
| Player intent | yes | rules | rules |
| Trade reads | yes | rules | rules |
| Speech gate | yes | rules | rules |

These choices come from side-by-side tests of all three models on the same
game states. Jev was right on every task. Von and Laya were right on most fight
stances asked as yes/no questions, but on the other tasks their answers were
near even or often wrong.

A fight stance goes out as one yes/no question for each stance the character
can carry out, and the clearest yes wins. A weak yes, or two yeses too close
to call, leaves the stance to the rules.

When the rules keep the speech gate, the line gets a chat reply, the same as
`"speechGate": false`. When the rules keep player intent, the player gets a
chat reply.

When Von or Laya leaves a big-moment decision to the rules, the chat provider
on `route.chat` can decide it instead. A big moment is, for example, an attack
by a player, the end of a dungeon run, or a player standing near. The chat
provider helps only while its replies come back within `chatHelpMaxSeconds` on
average (5 by default). A slower provider, a paused one, or one over its paid
budget does not help, and the rules decide. Some picks always stay with the
rules: the next-job pick at the end of each job (the character stands still
until that answer comes), multi-step plans, and the red moments. Set
`"chatHelpMaxSeconds": 0` to turn this help off.

A `family` key on the provider names the model. Without one, a `baseUrl` on
`typesafe.ai`, or a model or provider name that starts with `jev`, is Jev. A
model or provider name that starts with `laya` is Laya. Any other System One
provider gets the Von trust. The console names the result at boot:

```text
Brain provider von runs Von: it may decide fight stances; the rules keep next jobs, player intent, trade reads, the speech gate
```

### Big moments

With a System One provider on the decision route, `jevScope` sets what it
decides. `"combat-and-big"` uses it for fights that matter and big moments.
`"all"` also sends every routine next job to it.

A big moment is a next job soon after the character comes back from death, or
while a real player stands within 12 tiles. Each counts once: a character
stays in the same moment until it ends. The attacked, dungeon-ended, and
player-noticed events and a player's own words (intent, haggle) count too.
Every other next job (work, hunt, bank, go home, cook, idle, walk) stays with
the scorer and is logged as `(scorer: routine job, ...)`.

At a big moment the scorer ranks the jobs the rules allow, and the provider
chooses among the best five from a short, word-only state (hits, mana, purse,
pack, time since the last hunt, rest, and bank, danger, party, want, and the
moment). The character stands until the answer comes. An error, a wait over
three seconds, an answer below the confidence floor, the hourly token budget,
or the rate limit gives the scorer's pick. Each pick is logged with the
provider name or a fallback reason.

### Fight stances

A fight that matters asks the provider on its fight use for a stance: press or
go back in, kite, step back and heal, paralyze then kite, protect, flee, or
hold. A fight matters when it is against a person, against a foe clearly
stronger than the character dares alone, or when the character is below half
its hits. The request carries only a few lines of words (how hurt, how near,
whether the trade of blows is being won, light damage, cornered, how often a
spell broke). It asks at most once every five seconds per character and six
times a fight, and only when those words changed. The combat rules carry the
stance out. A blue fighting a red presses while it wins the trade (the model is
not asked then) and kites only when it is losing.

### Red moments

Three moments in red life each go out as one choice. The hard rules (guards,
reach, what the character can do) make the options first, and the model picks
only from them. The request uses the `bigMoment` route, and a paid provider
spends the Jev hourly token budget on it. Only Jev decides these moments. With
Von or Laya on `bigMoment`, the rule pick stands.

| Moment | Options | Rule pick |
|---|---|---|
| A red is raised near its body, and both a guard-free walk to the body and a recall home are open | Walk back to the body now, or recall home to the Den and re-arm | Walk back |
| A blue band is ready to raid Buccaneer's Den | Raid now, or hold | Raid |
| A red ghost has two or three ways back | Call a free red, walk to the ankh with the shortest trip, or wait by the body for a busy gang mate | Call, then the ankh, then the wait |

Each moment is asked once, and the character waits at most 3 seconds for the
answer. No provider, a refused call, no answer, a weak answer, or a late
answer gives the rule pick. A ghost that chooses to wait calls again after
1 minute, then follows the rule order. A red that must re-arm in the Den does
not ask for its next job: the rules send it home first.

### Mixed setups

| Setup | `route` keys | Who decides |
|---|---|---|
| Jev for everything | `"decision": "jev"` | Jev decides every task. Routine next jobs go to Jev only with `"jevScope": "all"`. |
| Free only | `"decision": "von"` or `"decision": "laya"` | The model decides fight stances. The rules decide the rest, with chat help at big moments. |
| Free fights, Jev for the rest | `"decision": "jev"`, `"decisionUses": { "personFight": "von", "monsterFight": "von" }` | Von decides fights at no cost. Jev decides the other tasks. |
| Free fights, Jev for big moments | `"decision": "von"`, `"decisionUses": { "bigMoment": "jev" }` | Von decides fights at no cost. Jev decides big moments, player intent, and red moments. The rules decide trade reads and the speech gate. |

## Top-level keys

| Key | Default | What it does |
|---|---|---|
| `enabled` | `true` | `false` turns every model call off. The rules run everything. |
| `providers` | `frontier`, `local`, `jev`, `von`, `laya` | The named providers. The names are yours; routes refer to them. The first-boot values match the scenarios below. |
| `route` | `chat` and `decision` set to `"frontier"` | See [Two routes](#two-routes). |
| `budget` | see below | Paid call caps for chat providers. |
| `maxConcurrentRequests` | `8` | Chat provider calls out at once. |
| `perCharacterCooldownSeconds` | `4` | The shortest time between two chat calls for one character. |
| `botToBotCooldownMinutes` | `2` | After a character answers another character, it waits this long before the next such reply. |
| `botToBotMaxExchanges` | `4` | Lines two characters trade before they stop. |
| `botToBotPairRestMinutes` | `10` | After a pair stops, it waits this long before it talks again. |
| `hearRange` | `6` | Tiles. Who hears a line. A reply can come from twice this range. |
| `maxReplyCharacters` | `160` | The longest reply a chat provider may write. |
| `temperature` | `0.8` | Sent to chat providers. Higher gives more varied words. |
| `failuresBeforePause` | `3` | Failures in a row before a provider pauses. Each provider counts its own failures. |
| `pauseAfterFailuresSeconds` | `60` | How long a failing provider pauses. Other providers keep working. |
| `attentionWindowSeconds` | `30` | How long a character keeps listening to a speaker after it is spoken to. |
| `decideCooldownMinutes` | `2` | The shortest time between two plan asks for one character. |
| `nearbyPlayerNoticeMinutes` | `3` | How often one character may notice an idle player near it. |
| `speechGate` | `true` | Character-to-character speech asks the `speechGate` provider before a chat reply is paid for. Player speech never goes through the gate. |
| `speechGateThreshold` | `0.5` | 0 to 1. A gate answer at or over this value gets a reply. A failed gate lets the line through. |
| `personaWriter` | `true` | `false` turns the persona writer off and ignores `personas-generated/`. Every copy then uses only the part library. |
| `personaWritesPerMinute` | `3` | Personas the chat provider writes each minute. `0` stops new writes and keeps the saved ones. |
| `jevScope` | `"combat-and-big"` | What a System One decision provider decides. `"combat-and-big"`: fights that matter and big moments. `"all"`: every next job too. Any other value reads as `"combat-and-big"`. |
| `jevMaxConcurrentRequests` | `32` | System One calls out at once, counted apart from `maxConcurrentRequests`. As many again wait in a queue. Past that, a call falls back to the rules (`jev busy` in the log for a next job). |
| `decisionMinConfidence` | `0.5` | 0 to 1. A System One choice (a next job, an event decision, player intent, a red moment) below this is not used. A provider's own higher `minConfidence` wins. Fight stances, trade reads, and the speech gate use their own floors. |
| `jevInputTokensPerHour` | `2380952` (about $0.10 an hour at the Jev price) | The input token budget for paid System One calls in one clock hour. Past 80%, only person fights, big moments, and trade reads go out. At 100%, none go out until the next hour. Free providers do not spend it. |
| `jevDecideCooldownSeconds` | `20` | After one System One next-job ask, that character's picks go to the rules for this long. |
| `chatHelpMaxSeconds` | `5` | The chat provider decides big moments that Von or Laya leaves to the rules, while its average reply time stays at or under this many seconds. `0` turns this help off. |

A value out of range goes back to its default at boot: a threshold or
confidence outside 0 to 1, a `chatHelpMaxSeconds` below 0, and a
`jevInputTokensPerHour`, `jevDecideCooldownSeconds`,
`jevMaxConcurrentRequests`, `maxPaidCallsPerDay`, or `maxPaidCallsPerHour` of
0 or less.

## Provider keys

Each entry in `providers` has these keys:

| Key | Default | What it does |
|---|---|---|
| `api` | `null` | `null` or `"openai"` for a chat provider. `"systemone"` for Jev, Von, or Laya. |
| `baseUrl` | none | The endpoint address. A chat provider's address ends before `/chat/completions`, for example `http://127.0.0.1:11434/v1`. A System One address ends before `/v1/systemone`, for example `http://127.0.0.1:8001`; an address that ends in `/v1` also works. |
| `model` | none | The model name sent with each call. Laya takes `"auto"`. |
| `apiKeyEnvironmentVariable` | `null` | The name of an environment variable that holds the key. It wins over `apiKey`. |
| `apiKey` | `null` | A key written in the file. Use the environment variable instead for a real key. |
| `timeoutSeconds` | `20` | The HTTP timeout. A value of 0 or less takes the default. |
| `paid` | `null` | `true`: calls count against the budgets. `false`: free, no key needed, never capped. `null`: a loopback address (`127.0.0.1`, `localhost`) is free, any other address is paid. Set `false` for a model on another machine on your LAN. |
| `thinking` | `false` | Chat providers only. `false` asks the model not to think before it answers, because a thinking model can spend seconds, or minutes, on one simple line. `true` lets it think. The plugin sends the switch in the field each service reads: `reasoning_effort` for OpenAI-style servers and Ollama, and `reasoning.effort` for OpenRouter. If a model refuses the switch (HTTP 400 or 422, and a plain retry works), the console warns once and the model gets plain requests. |
| `reasoningEffort` | `null` | Chat providers only, used only with `"thinking": true`. The thinking level to send, such as `"low"` or `"high"`. `null` sends no level, so the model uses its own default. |
| `minConfidence` | `null` | System One providers only. This provider's floor for choices. The higher of this and `decisionMinConfidence` is used. |
| `family` | `null` | System One providers only. `"jev"`, `"von"`, or `"laya"`. It sets which tasks the model decides. See [What each System One model decides](#what-each-system-one-model-decides). `null` detects the model. |

A provider without a `baseUrl` or `model`, or without a key, is turned off at
boot with a warning. A free provider (`paid: false` or a loopback address)
needs no key. A route that names a missing provider turns that call kind off
with a warning. Only providers named by a route start; the other entries do
nothing.

## Budget keys

The `budget` object caps paid chat provider calls. System One calls use
`jevInputTokensPerHour` instead.

| Key | Default | What it does |
|---|---|---|
| `maxPaidCallsPerDay` | `10000` | Paid chat calls in a rolling day. |
| `maxPaidCallsPerHour` | `10000` | Paid chat calls in a rolling hour. |
| `countLocalAsPaid` | `false` | `true` counts every provider as paid, local ones too. |

The counts start at zero on each boot. At the cap, the character uses the
rules and the log shows `Brain cap reached`.

Characters chat to each other only through a free chat provider, and only
while a real player is within 15 tiles to hear it. With a paid `route.chat`,
characters answer players but not each other.

## Scenarios

Each example shows only the keys it changes. Put them into your existing
`providers` and `route` objects and keep the other keys you have.

### No models

```json
{ "enabled": false }
```

### One paid cloud model for everything

```json
{
  "providers": {
    "frontier": { "baseUrl": "https://openrouter.ai/api/v1", "model": "deepseek/deepseek-chat", "apiKeyEnvironmentVariable": "SOSARIAAI_API_KEY", "timeoutSeconds": 20, "paid": true }
  },
  "route": { "chat": "frontier", "decision": "frontier" }
}
```

Set the key before boot: `export SOSARIAAI_API_KEY=...`. Characters answer
players. They do not chat to each other, because the chat provider is paid.

### One free local model for everything

```json
{
  "providers": {
    "local": { "baseUrl": "http://127.0.0.1:11434/v1", "model": "llama3.1:8b", "apiKey": "ollama", "timeoutSeconds": 30, "paid": false, "thinking": false }
  },
  "route": { "chat": "local", "decision": "local" }
}
```

Characters also chat to each other.

### A model on another machine on your LAN

```json
{
  "providers": {
    "gpu": { "baseUrl": "http://192.168.1.50:11434/v1", "model": "gemma4:e4b", "apiKey": "ollama", "timeoutSeconds": 30, "paid": false, "thinking": false }
  },
  "route": { "chat": "gpu", "decision": "gpu" }
}
```

You need `paid: false` here. Without it, a LAN address counts as paid and
needs a key.

### Chat model for words, Jev for decisions

```json
{
  "providers": {
    "jev": { "api": "systemone", "baseUrl": "https://api.typesafe.ai", "model": "jev-latest", "apiKeyEnvironmentVariable": "SOSARIAAI_JEV_KEY", "timeoutSeconds": 20, "paid": true }
  },
  "route": { "chat": "frontier", "decision": "jev" },
  "jevScope": "combat-and-big",
  "jevInputTokensPerHour": 2380952
}
```

Set the key before boot: `export SOSARIAAI_JEV_KEY=...`. Plans come from the
rules. Use `"jevScope": "all"` to send every next job to Jev too, which spends
more tokens.

### Von for decisions

```json
{
  "providers": {
    "von": { "api": "systemone", "baseUrl": "http://127.0.0.1:8000", "model": "von-1.2.0", "timeoutSeconds": 20, "paid": false }
  },
  "route": { "chat": "frontier", "decision": "von" }
}
```

Run Von: `pip install von-sdk`, then
`von serve --host 127.0.0.1 --port 8000`.

### Laya for decisions

```json
{
  "providers": {
    "laya": { "api": "systemone", "baseUrl": "http://127.0.0.1:8001", "model": "auto", "timeoutSeconds": 20, "paid": false }
  },
  "route": { "chat": "frontier", "decision": "laya" }
}
```

Run Laya: `pip install "laya[serve]"`, then
`LAYA_HOST=127.0.0.1 LAYA_PORT=8001 laya-serve`. Its `/health` page shows
`"status": "ok"` when it is ready. If Laya uses bearer auth, set
`apiKeyEnvironmentVariable` and set that variable before boot.

### Von or Laya on another machine

```json
{
  "providers": {
    "von": { "api": "systemone", "baseUrl": "http://192.168.1.50:8013", "model": "von-latest", "timeoutSeconds": 20, "paid": false },
    "laya": { "api": "systemone", "baseUrl": "http://192.168.1.50:8012", "model": "auto", "timeoutSeconds": 20, "paid": false }
  }
}
```

Keep `paid: false`. Start the service with `--host 0.0.0.0` (Von) or
`LAYA_HOST=0.0.0.0` (Laya) so other machines can reach it.

### Different decision models per use

```json
{
  "route": {
    "chat": "frontier",
    "decision": "von",
    "decisionUses": { "personFight": "jev", "bigMoment": "jev", "monsterFight": "laya" }
  }
}
```

Person fights, big moments, player intent, and red moments go to Jev. Monster
fights go to Laya. Trade reads and the speech gate follow `route.decision` to
Von, which leaves them to the rules.

### Speech gate off

```json
{ "speechGate": false }
```

Every character-to-character line that passes the other checks gets a chat
reply. Use this when the chat provider is free and you want more talk.

### A provider gives too many fallbacks

Jev, Von, and Laya each send their own `confidence` field, and each computes it
in a different way. Von sends the gap between the top two probabilities. Laya
sends 1 minus the normalised entropy. The plugin uses that field only when an
answer carries no `probabilities`. Otherwise it computes Jev's measure from the
`probabilities` that all three send: the pick's probability, rescaled so an
even spread is 0 and a certain pick is 1. One floor then means the same thing
for each provider.

When the activity log shows many choices below the confidence floor
(`fallback: jev unsure 0.12`), set a lower floor:

```json
{ "decisionMinConfidence": 0.3 }
```

To change only one provider, leave `decisionMinConfidence` low and set that
provider's `minConfidence` higher.

### Hard cap on paid chat calls

```json
{
  "budget": { "maxPaidCallsPerDay": 2000, "maxPaidCallsPerHour": 150, "countLocalAsPaid": false }
}
```

## Connections

Each provider opens a fresh connection for each request and keeps none idle. A
request that fails on the wire is tried once more after a second, and the
activity log names the reason: `von request failed (ConnectionError); trying
once more`. A second failure gives the brain warning with the same reason.
System One calls are paced to 1200 a minute and back off after a 429 or 529.

## What the console shows

- `Brain provider <name> is on: model <model> at <baseUrl>`: one line for each
  provider that started.
- `Brain provider <name> runs <family>: it may decide <tasks>`: for each System
  One provider, with the tasks the rules keep.
- `Brain provider <name> leaves next jobs to the rules; <chat> decides big moments while it answers within <n> s`:
  chat help is on for that provider.
- `Brain provider <name> has no key and is not a local endpoint`: that
  provider is off. Set its key or set `paid: false`. A provider with no
  `baseUrl` or `model` gets the same line.
- `Unknown brain provider <name> in the route`: a route names a provider that
  is not in `providers`.
- `Brain is off: enabled is false in brain.json` or
  `Brain model is off: no usable provider`: no model runs, and the rules run
  everything.
- `Brain provider <name> paused for <n> seconds`: that provider failed
  `failuresBeforePause` times in a row.
- `jev usage: N requests, M input tokens, ~$X this hour (...)`: once an hour
  and at shutdown, for paid System One calls. The part in brackets splits the
  requests and input tokens by kind (decision, big moment, stance against a
  person, stance against a monster, trade, speech gate).
- With `logActivity` on in `characters.json`, `activity.log` names the
  provider for each pick, or the fallback reason.
