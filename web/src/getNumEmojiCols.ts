// Each column and each emoji row is 1.3 times the emoji size.
const CELL_RATIO = 1.3;

// The emoji size rule exists in two places: in CSS (--emoji-size in App.css)
// and here. CSS draws the emojis, but only JS can turn the size into a column
// count. Keep both rules equal: clamp(110px, 14vw, 200px).
export const getEmojiSize = (viewportWidth: number) =>
  Math.min(200, Math.max(110, viewportWidth * 0.14));

export const getEmojiLayout = (
  viewportWidth: number,
  viewportHeight: number,
) => {
  const emojiSize = getEmojiSize(viewportWidth);
  const cellSize = emojiSize * CELL_RATIO;

  return {
    emojiSize,
    numColumns: Math.max(1, Math.floor(viewportWidth / cellSize)),
    // The extra row makes one copy of a column taller than the viewport.
    rowsPerColumn: Math.ceil(viewportHeight / cellSize) + 1,
  };
};
