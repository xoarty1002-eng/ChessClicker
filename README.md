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
4. The resizable main window keeps calibration, **Scan FEN**, Play, Settings, the board preview, multi-line input, status, and log visible together. Enter a coordinate move (for example `e2e4` or `a8-a6`; capture `x` and promotion suffixes are also accepted) and press Enter, **F3**, or **Analyze / Click** to send those square clicks using the detected board orientation. Use **Register opponent move** with the same input to record a move already made on the visible board without clicking it; the app determines the mover's color from the piece on the source square and updates whose turn is next. Instead paste a full six-field FEN into the same input: the app validates and loads that position, then returns an engine suggestion without clicking. Play continues from this loaded position rather than replacing it with the configured starting position.
5. Board orientation detection compares normalized piece-to-square contrast on the two board ends and refuses to guess when the evidence is ambiguous. Perspective is detected when Play starts and held steady while scanning so changing piece positions cannot reverse the screen mapping; stop and start Play again if you rotate the board. **Solo** makes the selected **Top** or **Bottom** screen side the engine side, and the other side is played by a person. The standard initial piece layout always starts with White to move; when Play starts, the app corrects a black-to-move FEN if the board is still in that untouched standard layout. On first Play start, it can also reconstruct one unambiguous, non-capturing legal move from the configured starting FEN by comparing piece and empty-square appearance. If it cannot safely infer a move, it keeps the configured FEN and logs the issue; you can register a known move or load the actual current FEN. **Duo** makes the same configured UCI engine play both sides until mate/stalemate or Play is stopped. Play automatically stops when the tracked position reaches checkmate or stalemate. Toggle Play or press **F2** to stop.
6. Use **Settings** to choose Solo/Duo mode, engine screen side, a UCI engine executable path (leave blank to auto-download Stockfish), engine thinking time, Stockfish skill (used only if the UCI engine advertises that option), minimum stable-board time before clicking (default 500 ms), scanning FPS, smoothing, brightness sensitivity, minimum piece-signature separation for Scan FEN, theme, starting FEN, and crop refinement. The default 10 FPS samples approximately every 100 ms, giving five observation intervals during the 500 ms stable-board window; the app calculates a recommended rate as `ceil(5000 / stable milliseconds)`, capped at 30 FPS. This is a temporal sampling recommendation, not a measured machine-specific optimum. Settings are saved to the per-user `settings.json` file and loaded on startup. Preview smoothing changes display resampling only; it does not change screen captures or move detection.
7. For **Scan FEN**, first calibrate the board and enter the exact FEN of the currently visible position in the input; a blank input is rejected so a stale configured starting FEN is not used as labels for a different game position. The scan waits for a stable square capture, then uses the supplied FEN as ground-truth piece labels and measures each occupied square's local-background-normalized 8×8 brightness/shape signature. It reports missing piece classes or whether the closest signatures meet the configured separation threshold. Use a position containing both colors of all six piece types for a complete calibration scan. This is a separability diagnostic, not full FEN recognition; startup reconstruction only compares occupied squares with legal single moves from the configured start.
8. The scanner compares against the last settled board and waits for the configured stable-board duration before accepting a detected move. It forms move candidates only from changed squares currently occupied by pieces, then tests changed destination squares against legal chess moves; the piece on the source square identifies the mover. When noisy changes make candidates ambiguous, a unique legal move that produces checkmate can still be confirmed, including when an end-of-game overlay obscures part of the board. Other ambiguous moves require matching detected piece occupancy and are not guessed. Mouse clicks are only treated as hints: the tracked position advances only after the screenshot confirms the move, so a rejected click cannot desynchronize the turn. Before sending an engine move, the app requires the board to remain stable for the configured time and a minimum of two captures; after sending, it waits for the same stable interval and confirms the expected move before advancing. On timeout, the log includes changed crop squares, click coordinates, and board bounds to help diagnose calibration or click delivery.

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

These tests verify square-to-pixel mapping in both board orientations, reverse coordinate mapping, ordered mouse down/up actions, cursor restoration, 8×8 grid fitting, brightness-shift normalization, unique legal-candidate selection, single-move position reconstruction, chess perft move counts, castling, en passant, promotions, king safety/checkmate, minimum stable timing, settings persistence, move-vs-FEN input, piece-signature separability, and UCI startup/options/bestmove handling using fake UCI processes. To run a real UCI-engine smoke test, set `CHESSCLICKER_TEST_UCI_ENGINE` to its executable path and filter for `OptInInstalledEngineReturnsALegalStartingPositionMove`. The mouse tests use fake input and do not send real desktop events or verify a real site's board.

## Current limitations

- The board model validates ordinary legal chess moves, castling, en passant, promotion, king safety, check, and checkmate. It does not track repetition, the 50-move draw, or insufficient material; it should not be treated as a full tournament arbiter.
- The processing pattern is **Engine → Board → Scanner → Board**: the Engine produces a UCI suggestion from the current FEN, the Board validates/records chess state, the Scanner observes screen changes and confirms an attempted move, and the Board advances only after confirmation. In human-vs-engine mode, the scanner first detects the human move, the board validates it, and only then is the engine queried. The FEN signature scan uses the supplied FEN as labels and checks whether the current image separates piece appearances; it is not image-only piece recognition.
- Board orientation is inferred from piece contrast on the outer two ranks, relative to each square's local background; if there is insufficient visible piece evidence, scanning stops with an error instead of guessing. Board-state scanning still compares pixel brightness changes and is not general-purpose piece recognition. Site themes, animations, highlights, scaling, and browser chrome can affect results.
- The calibrated capture is divided proportionally into an 8×8 grid, including any remainder pixels. **Auto-fit** searches nearby crop bounds for the strongest grid-edge alignment. The scanner subtracts the median brightness shift across all 64 cells to suppress global lighting changes, enumerates moves from changed cells, and accepts a detected move only when exactly one candidate passes the chess legality validator. Ambiguous changes are rejected; highlights, animation, theme changes, and piece-identification ambiguity can still prevent reliable detection.
- Settings control Stockfish analysis strength and thinking time only. There is no human-mimicry mode or feature intended to conceal engine assistance or evade fair-play detection.
- Engine moves are synchronized only after a stable screen change matching the requested move is observed. This visual confirmation is heuristic, not proof that a site accepted the move; verify the board and game state yourself. Promotion selection depends on the target site's UI and is not verified end-to-end.
- The desktop mouse hook, screen capture, and engine downloader are Windows-specific. Linux compatibility is limited to cross-building the Windows target and running the portable unit tests.

## License

ChessClicker is licensed under the GNU Affero General Public License, version 3 only (AGPL-3.0-only). See [LICENSE.txt](LICENSE.txt) for the complete license text.

Stockfish is downloaded separately at runtime and is not included in this repository. Stockfish is distributed under its own GPLv3 license; consult the license and notices accompanying the exact Stockfish release you download. This project's AGPL license does not replace or change Stockfish's license.
