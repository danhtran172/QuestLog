import assert from "node:assert/strict";
import test from "node:test";
import { CONSOLE_SOUND_CUES } from "../src/lib/consoleSounds.ts";

function cueLength(notes) {
  return Math.max(...notes.map((note) => note.at + note.duration));
}

test("every console sound cue has valid notes", () => {
  for (const [cue, notes] of Object.entries(CONSOLE_SOUND_CUES)) {
    assert.ok(notes.length > 0, `${cue} has no notes`);
    for (const note of notes) {
      assert.ok(note.at >= 0, `${cue} starts before zero`);
      assert.ok(note.duration > 0, `${cue} has an empty note`);
      assert.ok(note.gain > 0 && note.gain <= 0.5, `${cue} gain out of range`);
      assert.ok(note.frequency >= 80 && note.frequency <= 6000, `${cue} frequency out of range`);
      if (note.frequencyEnd !== undefined) assert.ok(note.frequencyEnd > 0, `${cue} glides to zero`);
    }
  }
});

test("navigation cues finish before the held-direction repeat fires again", () => {
  // controllerInput lap lai moi 110ms khi giu huong: cue di chuyen phai tat truoc do.
  for (const cue of ["move", "blocked"]) {
    assert.ok(cueLength(CONSOLE_SOUND_CUES[cue]) < 0.11, `${cue} overlaps the next repeat`);
  }
});

test("enter and exit console cues stay short", () => {
  assert.ok(cueLength(CONSOLE_SOUND_CUES.enterConsole) <= 0.7);
  assert.ok(cueLength(CONSOLE_SOUND_CUES.exitConsole) <= 0.7);
});
