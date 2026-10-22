import react, { reactCompilerPreset } from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import babel from "@rolldown/plugin-babel";

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({
      presets: [reactCompilerPreset()],
    }),
  ],
  server: { proxy: { "/api": "http://localhost:5000" } },
});
