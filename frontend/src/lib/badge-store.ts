// Lightweight vanilla subscriber store for real-time sidebar badge values
// pushed from the backend via SignalR (`INotificationClient.UpdateBadge`).
//
// A value of '0' is treated as "don't show" by consumers. Setting a badge to
// '0' still notifies subscribers so the UI can hide it on transition.

type BadgeMap = Record<string, string>
type Listener = (values: BadgeMap) => void

let state: BadgeMap = {}
let listeners: Listener[] = []

function notify() {
  listeners.forEach((fn) => fn(state))
}

export const badgeStore = {
  get: (): BadgeMap => state,

  set(id: string, value: string) {
    if (state[id] === value) return
    state = { ...state, [id]: value }
    notify()
  },

  clear() {
    if (Object.keys(state).length === 0) return
    state = {}
    notify()
  },

  subscribe(listener: Listener): () => void {
    listeners.push(listener)
    return () => {
      listeners = listeners.filter((fn) => fn !== listener)
    }
  }
}
