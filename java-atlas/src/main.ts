import { mount } from "svelte";
import App from "./App.svelte";
import "./app.css";
import { applyTheme, loadTheme } from "./lib/theme";

// Before the first paint, so a dark-mode user never sees a white flash.
applyTheme(loadTheme());

const app = mount(App, { target: document.getElementById("app")! });

export default app;
