// Service worker của POS: chỉ cache app shell (HTML, JS, CSS, icon). Không cache phản hồi API và không xếp hàng đợi
// đơn khi mất mạng (docs/design/ui/pos.md mục "PWA", ADR-0007). Đổi chiến lược cache thì tăng số phiên bản.
const CACHE = 'oism-pos-shell-v1';
const SHELL = ['/', '/manifest.webmanifest', '/icon-192.png', '/icon-512.png'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => cache.addAll(SHELL))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

function store(request, response) {
  if (response.ok) {
    const copy = response.clone();
    void caches.open(CACHE).then((cache) => cache.put(request, copy));
  }
  return response;
}

self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);
  // Chỉ tài nguyên của chính POS. API nằm ở gateway, khác origin, nên không bao giờ đi qua cache này.
  if (request.method !== 'GET' || url.origin !== self.location.origin) return;

  if (request.mode === 'navigate') {
    // HTML lấy mới khi có mạng để nhận bản build mới; mất mạng thì mở bản đã cache để hiện băng báo mất mạng.
    event.respondWith(
      fetch(request)
        .then((response) => store('/', response))
        .catch(() => caches.match('/')),
    );
    return;
  }

  // JS và CSS do Vite build mang hash trong tên nên nội dung không đổi: lấy từ cache trước.
  if (url.pathname.startsWith('/assets/') || SHELL.includes(url.pathname)) {
    event.respondWith(caches.match(request).then((cached) => cached ?? fetch(request).then((response) => store(request, response))));
  }
});
