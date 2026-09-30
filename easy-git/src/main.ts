import { mount } from "svelte";
import App from "./App.svelte";
import { view } from "./lib/view.svelte";
import "./app.css";

// Apply the saved theme before the first paint, so a dark-mode user never
// sees a white flash.
view.applyTheme();

const app = mount(App, { target: document.getElementById("app")! });

export default app;
