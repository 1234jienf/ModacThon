# Hackathon AI Map Analysis

Unity exports each run to:

```text
HackathonAI/runs/<timestamp>/
  request.json
  map_a/
    ascii.txt
    structure.json
    screenshot.png
  map_b/
    ascii.txt
    structure.json
    screenshot.png
  analysis_result.json
```

Set the API key outside Unity:

```bash
cp .env.example .env
# .env 파일을 열어 OPENAI_API_KEY에 본인 키 입력
```

Or export directly:

```bash
export OPENAI_API_KEY="your-new-key"
```

Run manually:

```bash
python3 HackathonAI/tools/analyze_maps.py HackathonAI/runs/<timestamp>/request.json
```

Unity can run this automatically through `HackathonAnalysisExporter` when `Run Python After Export` is enabled.
