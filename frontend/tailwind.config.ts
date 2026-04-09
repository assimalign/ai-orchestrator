import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: {
          950: "#07090d",
          900: "#0b0f14",
          850: "#10161d",
          800: "#131b23",
          700: "#1a2430",
        },
        sage: {
          50: "#edf7f1",
          100: "#d6ebdd",
          200: "#add6bb",
          300: "#7dbb95",
          400: "#55a173",
          500: "#3d865c",
          600: "#2f6948",
          700: "#244f37",
        },
        ember: {
          400: "#e6a76b",
          500: "#d7894f",
        },
      },
      boxShadow: {
        panel: "0 22px 80px rgba(0, 0, 0, 0.35)",
      },
      fontFamily: {
        sans: ['"Aptos"', '"Segoe UI Variable"', "system-ui", "sans-serif"],
        mono: ['"Cascadia Code"', '"IBM Plex Mono"', "monospace"],
      },
      backgroundImage: {
        "workspace-radial":
          "radial-gradient(circle at top left, rgba(86, 167, 122, 0.18), transparent 28%), radial-gradient(circle at 85% 0%, rgba(217, 137, 79, 0.08), transparent 20%), linear-gradient(180deg, #07090d 0%, #0d1218 100%)",
      },
    },
  },
  plugins: [],
};

export default config;
