import { useCallback, useEffect, useState } from "react";
import "./App.css";

type HealthResponse = { status: string };

function App() {
  const [data, setData] = useState<HealthResponse | undefined>(undefined);

  const loadData = useCallback(async () => {
    const response = await fetch("/api/health");

    if (!response.ok) {
      throw new Error("Lame");
    }

    const data = (await response.json()) as HealthResponse;

    console.log({ data });

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
        Wussup <div>{data?.status}</div>
      </article>
    </div>
  );
}

export default App;
