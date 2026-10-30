// Event global untuk membuka jendela Live Chat melayang dari mana saja
// (tombol dashboard, menu navbar, atau URL /live-chat).
export const OPEN_LIVE_CHAT_EVENT = 'open-live-chat'

export function openLiveChat() {
  window.dispatchEvent(new Event(OPEN_LIVE_CHAT_EVENT))
}
