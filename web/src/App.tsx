import { useCallback, useEffect, useState } from "react";
import "./App.css";

// type HealthResponse = { status: string };

// function isHealthResonse(value: unknown): value is HealthResponse {
//   if (
//     typeof value === "object" &&
//     value !== null &&
//     "status" in value &&
//     typeof value.status === "string"
//   ) {
//     return true;
//   } else {
//     return false;
//   }
// }

function App() {
  const [data, setData] = useState<{ character: string }[] | undefined>(
    undefined,
  );

  const loadData = useCallback(async () => {
    const response = await fetch("/api/emojis");

    if (!response.ok) {
      throw new Error("Lame");
    }

    const data = await response.json();

    setData(data);
  }, []);

  useEffect(() => {
    loadData();
  }, []);

  return (
    <div>
      <header>
        <fieldset id="color-mode-switcher" className="radio-switch">
          <legend>Select a color mode:</legend>
          <input type="radio" id="light" name="color-scheme" value="light" />
          <label htmlFor="light">Light</label>
          <input type="radio" id="dark" name="color-scheme" value="dark" />
          <label htmlFor="dark">Dark</label>
        </fieldset>
      </header>
      <article>
        <div>{data?.map((emoji) => emoji.character)}</div>
      </article>
    </div>
  );
}

export default App;
