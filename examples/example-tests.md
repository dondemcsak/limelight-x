## Purpose
This document defines the demo examples to use
---

# 1. Demo Tests

Make sure to run $env:ANTHROPIC_API_KEY = "    "   first with your Anthropic Key


CD to the location of the executable and make sure to have a sub folder with the examples in it along with the meeting.txt file

1. `.\llx run examples/summarize-for-slack.llx` (requires `meeting.txt` and `ANTHROPIC_API_KEY`)
2. `.\llx explain examples/summarize-to-json.llx` (no API key needed)
3. `.\llx trace examples/summarize-to-spanish.llx` (requires API key)



If you are running from the debug output folder

1. `.\llx run ../../examples/summarize-for-slack.llx` (requires `meeting.txt` and `ANTHROPIC_API_KEY`)
2. `.\llx explain ../../examples/summarize-to-json.llx` (no API key needed)
3. `.\llx trace ../../examples/summarize-to-spanish.llx` (requires API key)