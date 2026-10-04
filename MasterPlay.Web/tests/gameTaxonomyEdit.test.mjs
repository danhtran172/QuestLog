import assert from "node:assert/strict";
import test from "node:test";
import {
  addTaxonomyValue,
  addTaxonomyValues,
  canCreateTaxonomyName,
  removeTaxonomyValue,
  toggleTaxonomyValue,
} from "../src/lib/gameTaxonomyEdit.ts";

const item = (id, name) => ({ id, name, iconData: null, sortOrder: 0 });

const ACTION = item(1, "Action");
const RPG = item(2, "RPG");

test("appends a value and leaves the source array untouched", () => {
  const current = [ACTION];
  assert.deepEqual(addTaxonomyValue(current, RPG), [ACTION, RPG]);
  assert.deepEqual(current, [ACTION]);
});

test("adding a value the game already has changes nothing", () => {
  assert.deepEqual(addTaxonomyValue([ACTION, RPG], item(1, "Action")), [ACTION, RPG]);
});

test("removes only the value asked for", () => {
  assert.deepEqual(removeTaxonomyValue([ACTION, RPG], 1), [RPG]);
  assert.deepEqual(removeTaxonomyValue([ACTION, RPG], 99), [ACTION, RPG]);
});

test("ticking a draft value twice leaves the draft empty", () => {
  const once = toggleTaxonomyValue([], ACTION);
  assert.deepEqual(once, [ACTION]);
  assert.deepEqual(toggleTaxonomyValue(once, item(1, "Action")), []);
});

test("commits a whole batch of picks in one go", () => {
  assert.deepEqual(addTaxonomyValues([ACTION], [RPG, item(3, "Indie")]), [
    ACTION,
    RPG,
    item(3, "Indie"),
  ]);
});

test("a batch never duplicates what the game already has, nor itself", () => {
  const current = [ACTION];
  assert.deepEqual(addTaxonomyValues(current, [item(1, "Action"), RPG, item(2, "RPG")]), [ACTION, RPG]);
  assert.deepEqual(current, [ACTION]);
});

test("offers creation when the typed name matches nothing", () => {
  assert.equal(canCreateTaxonomyName("Roguelike", "Roguelike", [ACTION, RPG]), true);
});

test("refuses creation when the name already exists, ignoring case and padding", () => {
  assert.equal(canCreateTaxonomyName("  action  ", "action", [ACTION]), false);
});

test("refuses creation while the results still belong to the previous keystrokes", () => {
  // `search` di truoc, `query` chi duoi kip sau debounce — ket qua dang hien la
  // cua "Rog", quyet dinh tao dua tren no la quyet dinh tren danh sach sai.
  assert.equal(canCreateTaxonomyName("Rogue", "Rog", [ACTION]), false);
});

test("refuses creation on an empty name or before the first result arrives", () => {
  assert.equal(canCreateTaxonomyName("   ", "", [ACTION]), false);
  assert.equal(canCreateTaxonomyName("Roguelike", "Roguelike", undefined), false);
});
