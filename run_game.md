dotnet build
dotnet run

# Screenshot mode (saves debug_output/<case>_<room>.png and exits):
dotnet run --no-build -- --screenshot <case_id> <room_id> [journal|final|dialogue]

# Static level checks (no game launch, run from repo root):
python tools/verify_level.py
