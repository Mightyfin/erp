import { toast } from "sonner";

/**
 * One place for action feedback, so every screen says the same kinds of thing
 * in the same way.
 *
 * Rules this encodes:
 *  - Say what happened to WHICH record, not just "Saved".
 *  - Anything reversible offers the undo right there, not in a menu.
 *  - A failure never disappears on a timer — the user dismisses it.
 *  - Nothing here persists, so every message says so once, quietly.
 */

const MOCK_NOTE = "Demonstration build — nothing is saved.";

/**
 * A blocked message stays until it is dealt with, so it needs a stable id:
 * repeating the same block replaces it instead of stacking, and succeeding at
 * the thing that was blocked clears it. An error left on screen after the user
 * has fixed it is worse than no error at all.
 */
const BLOCKED = "blocked";

export const feedback = {
  /** A change that took effect and can be taken back. */
  saved(what: string, onUndo?: () => void) {
    toast.dismiss(BLOCKED);
    toast.success(what, {
      description: MOCK_NOTE,
      action: onUndo ? { label: "Undo", onClick: onUndo } : undefined,
    });
  },

  /** Something was submitted into a workflow and now sits with someone else. */
  submitted(what: string, nextStep: string) {
    toast.dismiss(BLOCKED);
    toast.success(what, { description: nextStep });
  },

  /** A deliberate, irreversible removal. */
  removed(what: string, onUndo?: () => void) {
    toast(what, {
      description: MOCK_NOTE,
      action: onUndo ? { label: "Undo", onClick: onUndo } : undefined,
    });
  },

  /** Blocked by a rule — explain the rule, not just the refusal. */
  blocked(what: string, why: string) {
    toast.error(what, { id: BLOCKED, description: why, duration: Infinity });
  },

  /** Worth knowing but not a failure. */
  note(what: string, detail?: string) {
    toast.info(what, { description: detail });
  },
};
