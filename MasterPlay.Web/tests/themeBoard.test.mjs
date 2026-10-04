import assert from "node:assert/strict";
import test from "node:test";
import { bucketKey, findBucketKey, moveGroup, moveTheme, orderRequest } from "../src/lib/themeBoard.ts";

const shared = (id) => ({ id, editable: false });
const mine = (id) => ({ id, editable: true });

const board = () => [
  { group: { id: 1 }, themes: [shared(10), shared(11), mine(12)] },
  { group: { id: 2 }, themes: [shared(20)] },
  { group: null, themes: [mine(30), mine(31)] },
];

test("bucket keys distinguish groups from the ungrouped bucket", () => {
  const [era, , ungrouped] = board();
  assert.equal(bucketKey(era), "g1");
  assert.equal(bucketKey(ungrouped), "none");
  assert.equal(findBucketKey(board(), 31), "none");
  assert.equal(findBucketKey(board(), 999), null);
});

test("moves a personal theme into another group before a given theme", () => {
  const before = board();
  const next = moveTheme(before, 30, "g1", 11);
  assert.deepEqual(next[0].themes.map((t) => t.id), [10, 30, 11, 12]);
  assert.deepEqual(next[2].themes.map((t) => t.id), [31]);
  assert.deepEqual(before[2].themes.map((t) => t.id), [30, 31], "input is not mutated");
});

test("appends at the end when there is no target theme", () => {
  const next = moveTheme(board(), 12, "g2", null);
  assert.deepEqual(next[1].themes.map((t) => t.id), [20, 12]);
  assert.deepEqual(next[0].themes.map((t) => t.id), [10, 11]);
});

test("reorders inside the same group", () => {
  const next = moveTheme(board(), 31, "none", 30);
  assert.deepEqual(next[2].themes.map((t) => t.id), [31, 30]);
});

test("shared themes and unknown groups are left untouched", () => {
  const before = board();
  assert.equal(moveTheme(before, 10, "none", null), before);
  assert.equal(moveTheme(before, 30, "g99", null), before);
});

test("the order request only lists editable themes, in display order", () => {
  const next = moveTheme(board(), 30, "g1", 11);
  assert.deepEqual(orderRequest(next, "g1"), { groupId: 1, ids: [30, 12] });
  assert.deepEqual(orderRequest(next, "none"), { groupId: null, ids: [31] });
});

test("groups reorder only among editable groups", () => {
  const groups = [{ id: 1, editable: false }, { id: 5, editable: true }, { id: 6, editable: true }];
  assert.deepEqual(moveGroup(groups, 6, 5).map((g) => g.id), [1, 6, 5]);
  assert.equal(moveGroup(groups, 6, 1), groups);
});
