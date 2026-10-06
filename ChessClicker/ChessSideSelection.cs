using System;

namespace ChessClicker
{
    public static class ChessSideSelection
    {
        public static bool IsWhiteAtScreenSide(bool isWhiteView, string screenSide)
        {
            ValidateScreenSide(screenSide);
            return screenSide == "Bottom" ? isWhiteView : !isWhiteView;
        }

        public static bool ShouldEngineMove(
            string playMode,
            string engineSide,
            bool isWhiteView,
            string activeColor)
        {
            if (playMode is not ("Solo" or "Duo"))
                throw new ArgumentException("Play mode must be Solo or Duo.", nameof(playMode));
            ValidateScreenSide(engineSide);
            if (activeColor is not ("white" or "black"))
                throw new ArgumentException("Active color must be white or black.", nameof(activeColor));
            if (playMode == "Duo")
                return true;

            bool engineIsWhite = IsWhiteAtScreenSide(isWhiteView, engineSide);
            return (activeColor == "white") == engineIsWhite;
        }

        private static void ValidateScreenSide(string screenSide)
        {
            if (screenSide is not ("Top" or "Bottom"))
                throw new ArgumentException("Screen side must be Top or Bottom.", nameof(screenSide));
        }
    }
}
