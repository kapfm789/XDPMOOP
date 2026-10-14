// Âm báo của thông báo realtime, tạo bằng Web Audio nên không cần file âm thanh.
let context: AudioContext | undefined;

export function playChime() {
  try {
    context ??= new AudioContext();
    // Trình duyệt treo AudioContext tới khi người dùng đã tương tác với trang; đăng nhập là một lần tương tác.
    void context.resume();
    const start = context.currentTime;
    [880, 1320].forEach((frequency, index) => {
      const at = start + index * 0.15;
      const oscillator = context!.createOscillator();
      const gain = context!.createGain();
      oscillator.frequency.value = frequency;
      gain.gain.setValueAtTime(0.2, at);
      gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.3);
      oscillator.connect(gain).connect(context!.destination);
      oscillator.start(at);
      oscillator.stop(at + 0.3);
    });
  } catch {
    // Trình duyệt chặn hoặc không có âm thanh: popup vẫn hiện.
  }
}
