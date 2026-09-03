import { EventEmitter } from "events";

// Node's built-in EventEmitter isn't type-safe by default (on/emit accept
// any string + any args). This thin wrapper adds compile-time checking of
// event names and payload shapes, giving roughly the same safety C#'s
// strongly-typed `event Action<T>` fields provide — without pulling in an
// external dependency for something this small.
export class TypedEventEmitter<Events extends Record<string, any[]>> {
  private emitter = new EventEmitter();

  on<K extends keyof Events>(event: K, listener: (...args: Events[K]) => void): void {
    this.emitter.on(event as string, listener as (...args: any[]) => void);
  }

  off<K extends keyof Events>(event: K, listener: (...args: Events[K]) => void): void {
    this.emitter.off(event as string, listener as (...args: any[]) => void);
  }

  protected emit<K extends keyof Events>(event: K, ...args: Events[K]): void {
    this.emitter.emit(event as string, ...args);
  }
}
