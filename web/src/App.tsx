import { useEffect, useState } from "react";
import "./App.css";
import { getEmojiLayout } from "./getNumEmojiCols";

type Emoji = { character: string };

function isEmojiResonse(value: unknown): value is Emoji[] {
  if (!Array.isArray(value)) {
    return false;
  }

  if (value.length === 0) {
    return true;
  }

  const arrayVal = value[0];

  if (
    typeof arrayVal === "object" &&
    arrayVal !== null &&
    "character" in arrayVal &&
    typeof arrayVal.character === "string"
  ) {
    return true;
  } else {
    return false;
  }
}

const BASE_SPEED = 90; // CSS pixels per second

const fraction = (value: number) => value - Math.floor(value);

// Speed and start point come from the column index, not from Math.random().
// Random values would change on every render. Multiplying by an irrational
// number and keeping the fraction gives values that are stable and well spread.
const getColumnMotion = (index: number) => ({
  speedFactor: 0.7 + 0.6 * fraction((index + 1) * 0.6180339887),
  startFraction: fraction((index + 1) * 0.7548776662),
});

type EmojiColumnProps = {
  emojis: Emoji[];
  index: number;
  rows: number;
  cellSize: number;
};

function EmojiColumn({ emojis, index, rows, cellSize }: EmojiColumnProps) {
  // Wrap with modulo so a short list still fills every column.
  const column = Array.from(
    { length: rows },
    (_, row) => emojis[(index * rows + row) % emojis.length].character,
  );

  const { speedFactor, startFraction } = getColumnMotion(index);
  const duration = (rows * cellSize) / (BASE_SPEED * speedFactor);

  return (
    <div className="emoji-column">
      {/* The list is drawn twice. The strip moves by half its height (one
          full list), so the second copy lands exactly where the first began. */}
      <div
        className="emoji-strip"
        style={{
          animationDuration: `${duration}s`,
          animationDelay: `${-startFraction * duration}s`,
        }}
      >
        {column.map((character, row) => (
          <div key={row}>{character}</div>
        ))}
        {column.map((character, row) => (
          <div key={row + rows}>{character}</div>
        ))}
      </div>
    </div>
  );
}

function App() {
  const [emojis, setEmojis] = useState<Emoji[] | undefined>(undefined);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<boolean>(false);
  const [viewport, setViewport] = useState({
    width: window.innerWidth,
    height: window.innerHeight,
  });
  const [colorMode, setColorMode] = useState<"dark" | "light">(
    window.matchMedia("(prefers-color-scheme: dark)").matches
      ? "dark"
      : "light",
  );

  const toggleColorMode = () =>
    colorMode === "dark" ? setColorMode("light") : setColorMode("dark");

  // Get the emojis. Rewrite to utilize the use function?
  useEffect(() => {
    const controller = new AbortController();

    const loadData = async () => {
      setLoading(true);
      const response = await fetch("/api/emojis?count=200", {
        signal: controller.signal,
      });

      if (!response.ok) {
        throw new Error("Lame");
      }

      const data = await response.json();

      if (!isEmojiResonse(data)) {
        throw new Error("Unknown type.");
      }

      setEmojis(data);
      setLoading(false);
    };

    loadData().catch((err: unknown) => {
      // The cleanup aborts the request on purpose, so this is not a failure.
      if (controller.signal.aborted) {
        return;
      }
      console.error(err);
      setError(true);
    });

    return () => controller.abort();
  }, []);

  useEffect(() => {
    const onResize = () =>
      setViewport({ width: window.innerWidth, height: window.innerHeight });

    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  const { emojiSize, numColumns, rowsPerColumn } = getEmojiLayout(
    viewport.width,
    viewport.height,
  );

  return (
    <div className="wrapper">
      <header>
        <button
          className="color-mode-icon"
          type="button"
          onClick={toggleColorMode}
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="2rem"
            height="2rem"
            viewBox="0 0 24 24"
            className="svg-light-dark"
          >
            <path d="M0 0h24v24H0z" fill="none" />
            <path
              fill="currentColor"
              d="M7.5 2c-1.79 1.15-3 3.18-3 5.5s1.21 4.35 3.03 5.5C4.46 13 2 10.54 2 7.5A5.5 5.5 0 0 1 7.5 2m11.57 1.5l1.43 1.43L4.93 20.5L3.5 19.07zm-6.18 2.43L11.41 5L9.97 6l.42-1.7L9 3.24l1.75-.12l.58-1.65L12 3.1l1.73.03l-1.35 1.13zm-3.3 3.61l-1.16-.73l-1.12.78l.34-1.32l-1.09-.83l1.36-.09l.45-1.29l.51 1.27l1.36.03l-1.05.87zM19 13.5a5.5 5.5 0 0 1-5.5 5.5c-1.22 0-2.35-.4-3.26-1.07l7.69-7.69c.67.91 1.07 2.04 1.07 3.26m-4.4 6.58l2.77-1.15l-.24 3.35zm4.33-2.7l1.15-2.77l2.2 2.54zm1.15-4.96l-1.14-2.78l3.34.24zM9.63 18.93l2.77 1.15l-2.53 2.19z"
            />
          </svg>
        </button>
        <fieldset id="color-mode-switcher" className="radio-switch">
          <legend>Select a color mode:</legend>
          <input
            type="radio"
            id="light"
            name="color-scheme"
            value="light"
            checked={colorMode === "light"}
            onChange={() => setColorMode("light")}
          />
          <label htmlFor="light">Light</label>
          <input
            type="radio"
            id="dark"
            name="color-scheme"
            value="dark"
            checked={colorMode === "dark"}
            onChange={() => setColorMode("dark")}
          />
          <label htmlFor="dark">Dark</label>
        </fieldset>
      </header>
      {error ? (
        <div>Could not load emojis. Reload the page to try again.</div>
      ) : null}
      {loading && !error ? <div>Loading...</div> : null}
      {!loading && emojis && emojis.length > 0 ? (
        <article className="emoji-columns">
          {Array.from({ length: numColumns }, (_, index) => (
            <EmojiColumn
              key={index}
              emojis={emojis}
              index={index}
              rows={rowsPerColumn}
              cellSize={emojiSize * 1.3}
            />
          ))}
        </article>
      ) : null}
    </div>
  );
}

export default App;
