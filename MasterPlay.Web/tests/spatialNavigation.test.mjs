import assert from "node:assert/strict";
import test from "node:test";
import { findSpatialCandidate } from "../src/lib/spatialNavigation.ts";

const rect = (left, top, width = 100, height = 100) => ({
  left,
  right: left + width,
  top,
  bottom: top + height,
  width,
  height,
});

test("chooses the closest candidate in the requested direction", () => {
  const candidates = [rect(300, 0), rect(140, 0), rect(-200, 0)];
  assert.equal(findSpatialCandidate(rect(0, 0), candidates, "right"), 1);
});

test("ignores candidates behind the requested direction", () => {
  const candidates = [rect(-200, 0), rect(0, 180)];
  assert.equal(findSpatialCandidate(rect(0, 0), candidates, "down"), 1);
});

test("prefers alignment when distance is otherwise similar", () => {
  const candidates = [rect(140, 220), rect(180, 10)];
  assert.equal(findSpatialCandidate(rect(0, 0), candidates, "right"), 1);
});
