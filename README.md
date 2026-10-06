# ChessClicker

ChessClicker is a Windows desktop prototype that samples a calibrated chessboard, keeps a simplified position, asks Stockfish for a move, and can send that move through the Windows mouse. It is not a chess-site integration or a complete chess rules engine.

## Requirements and platform support

- Windows 10/11 with the .NET 10 Desktop Runtime.
- A local chessboard visible on screen and permission to control the desktop mouse.
- Internet access the first time Stockfish is installed. The current downloader looks for a Windows x64 Stockfish release.

The application uses Windows Forms, GDI screen capture, and Win32 mouse hooks. It cannot run as a desktop app on Linux. Linux can cross-build the Windows target in compatible mode, but that does not make Windows Forms or the mouse hook executable on Linux:

```sh
dotnet build ChessClicker.slnx
```

The project includes the Windows-targeting compatibility flag required for Linux cross-builds, so a normal `dotnet build` or `dotnet test` invocation can succeed in a CI or dev container that is not running Windows.

## Run

1. Build and start ChessClicker on Windows.
2. Open a local chessboard or a site's analysis board. Do not use engine assistance in a live rated or casual game; follow the site's fair-play rules.
3. Use **Manual calibrate**, then click the board's outer top-left and bottom-right corners in that order. Press **Esc** to cancel without replacing the previous calibration.
4. The window opens at 450×450 and can be resized from 100×100 up to the available desktop size. The main controls, move entry, status, preview, and log are in this window. Enter a coordinate move (for example `e2e4` or `a8-a6`; capture `x` and promotion suffixes are also accepted) in the move box and press Enter, **F3**, or **Click move (F3)** to send those square clicks using the detected board orientation.
5. Board orientation detection identifies which color is at the bottom. Perspective is detected when Play starts and held steady while scanning so changing piece positions cannot reverse the screen mapping; stop and start Play again if you rotate the board. On the first Play start, the configured starting-position FEN is loaded with that bottom-side color set to move, then Stockfish makes a move for that side. For an already-progressed game, first set the actual current board position in **Settings → Starting position FEN**; ChessClicker detects orientation and changed squares, not individual piece identities, so it cannot reconstruct a midgame position from the screenshot alone. The preview continues refreshing while engine work is in progress; toggle **Play** or press **F2** again to stop.
6. Use **Settings** to choose the starting position FEN, Stockfish thinking time (100–10,000 ms), skill level (0–20), preview/scanning rate (1–30 FPS), preview smoothing (0–100%, default 50%), brightness-change sensitivity, board theme, and whether to refine the calibrated board crop during play. When starting a game in progress, paste its current FEN here so the internal position and active side match the visible board. Preview smoothing changes display resampling only; it does not add detail to screen captures or alter move detection. Settings are saved to the per-user `settings.json` file and loaded on startup. Optionally randomize the Stockfish skill from 0–20 every turn or every N engine turns.
7. Before sending any move, the app requires three consecutive stable board captures (allowing small brightness noise); if the board keeps changing for eight seconds, the move is blocked and not sent. After sending, the app waits up to eight seconds for the captured board to show the move before updating its tracked position. If a uniquely detected move is legal only for the other side, the tracked turn is resynchronized and logged.

Calibration, screen capture, and mouse automation require a visible desktop. They do not work against a browser page fetched in the background.

## Using chess websites responsibly

Use ChessClicker only with a local board, a board you control, or an analysis/editor board where engine assistance is allowed. Never use it to obtain or execute engine moves during an active game against another person or in a way that violates a site's rules.

- [Chess.com Analysis](https://www.chess.com/analysis): open the analysis board to set up or review a position. For a live game, use Chess.com's own **Play** features without ChessClicker or engine assistance.
- [Lichess Analysis](https://lichess.org/analysis): use the analysis board to study positions and review moves. For a live game, use Lichess's **Play** features without ChessClicker or engine assistance.
- [Internet Chess Club](https://www.chessclub.com/) and other chess services: use each service's own board and built-in study or analysis features, and follow its fair-play policy. Interface labels and rules can change.

The chess article on [Wikipedia](https://en.wikipedia.org/wiki/Chess) contains historical illustrations and chess-related images, not an interactive board with a machine-readable position. Such images can be used as visual references, but they are not a valid end-to-end board-scanning test for ChessClicker.

## Tests

Run the portable mouse-coordinate and input-sequence unit tests on Linux or Windows:

```sh
dotnet test ChessClicker.Tests/ChessClicker.Tests.csproj
```

These tests verify square-to-pixel mapping in both board orientations, reverse coordinate mapping, ordered mouse down/up actions, cursor restoration, 8×8 grid fitting, brightness-shift normalization, unique legal-candidate selection, and invalid input handling. They use a fake mouse input and do not send real desktop events or verify a real site's board.

## Current limitations

- ChessClicker currently asks one Stockfish engine to respond to detected board moves; Play mode keeps scanning until stopped, but does not determine checkmate because the position model is not a complete chess rules arbiter. Move validation depends on the loaded FEN and the simplified rules implementation.
- The chess position model is simplified and does not implement all FIDE rules (including check legality, castling, en passant, promotion, repetition, or draw adjudication). Do not rely on it as a complete rules arbiter.
- Board orientation is inferred from piece contrast on the outer two ranks, relative to each square's local background; if there is insufficient visible piece evidence, scanning stops with an error instead of guessing. Board-state scanning still compares pixel brightness changes and is not general-purpose piece recognition. Site themes, animations, highlights, scaling, and browser chrome can affect results.
- The calibrated capture is divided proportionally into an 8×8 grid, including any remainder pixels. **Auto-fit** searches nearby crop bounds for the strongest grid-edge alignment. The scanner subtracts the median brightness shift across all 64 cells to suppress global lighting changes, enumerates moves from changed cells, and accepts a detected move only when exactly one candidate passes the current move validator. Ambiguous changes are rejected. The current move validator is simplified; verify positions manually.
- Settings control Stockfish analysis strength and thinking time only. There is no human-mimicry mode or feature intended to conceal engine assistance or evade fair-play detection.
- Engine moves are synchronized only after a stable two-square screen change matching the requested move is observed. This visual confirmation is heuristic, not proof that a site accepted the move; verify the board and game state yourself.
- The desktop mouse hook, screen capture, and engine downloader are Windows-specific. Linux compatibility is limited to cross-building the Windows target and running the portable unit tests.

## License

ChessClicker is licensed under the GNU Affero General Public License, version 3 only (AGPL-3.0-only). See [LICENSE.txt](LICENSE.txt) for the complete license text.

Stockfish is downloaded separately at runtime and is not included in this repository. Stockfish is distributed under its own GPLv3 license; consult the license and notices accompanying the exact Stockfish release you download. This project's AGPL license does not replace or change Stockfish's license.
