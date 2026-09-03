// Equivalent of the C# GameStartValidationResult (section 1.7). Lets the
// lobby/room code show a specific, actionable message before attempting to
// start the game, instead of only failing deep inside startGame().
export interface GameStartValidationResult {
  canStart: boolean;
  failReason?: string;
}

export function validationOk(): GameStartValidationResult {
  return { canStart: true };
}

export function validationFail(reason: string): GameStartValidationResult {
  return { canStart: false, failReason: reason };
}
