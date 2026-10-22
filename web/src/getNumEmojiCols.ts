const smileyEmoji = "😀";

export const getNumEmojiCols = () => {
  const boundingContainer = document.getElementsByClassName("emoji-columns")[0];
  const emojiColumn = document.createElement("div");
  emojiColumn.innerText = smileyEmoji;

  const containerWidth = boundingContainer.getBoundingClientRect().width;
  const emojiWidth = emojiColumn.getBoundingClientRect().width;

  const numColumns = Math.floor(containerWidth / emojiWidth);

  return numColumns;
};
