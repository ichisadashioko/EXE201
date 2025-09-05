// The version of the cache
const VERSION = 'v0.0.1';
const CACHE_NAME = `tinder_for_pets_${VERSION}`;
// Files to cache
const APP_STATIC_RESOURCES = [
    '/',
    '/index.html',
    '/style.css',
    '/app.js',
    '/favicon.ico',
    '/manifest.json',
    '/icon-512.png',
];

// on install, cache static resources
self.addEventListener('install', (event) => {
    event.waitUntil(
        (async () => {
            const cache = await caches.open(CACHE_NAME);
            await cache.addAll(APP_STATIC_RESOURCES);
            // await self.skipWaiting();
        })(),
    );
});

// delete old caches on activate
self.addEventListener('activate', (event) => {
    event.waitUntil(
        (async () => {
            const cacheNames = await caches.keys();
            await Promise.all(
                cacheNames.map((name) => {
                    if (name !== CACHE_NAME) {
                        return caches.delete(name);
                    }

                    return undefined;
                }),
                // cacheNames
                //     .filter((name) => name !== CACHE_NAME)
                //     .map((name) => caches.delete(name)),
            );
            await self.clients.claim();
        })(),
    );
});

// on fetch, intercept server requests and respond with cached responses instead of going to network
self.addEventListener('fetch', (event) => {
    // when seeking an HTML page
    if (event.request.mode === 'navigate') {
        // return to the index.html page
        event.respondWith(caches.match('/'));
        return;
    }

    // for every other request type
    event.respondWith(
        (async () => {
            const cache = await caches.open(CACHE_NAME);
            const cachedResponse = await cache.match(event.request.url);
            if (cachedResponse) {
                return cachedResponse;
            }

            // response with a HTTP 404 response status
            return new Response('sw.js: Not found', { status: 404 });
            // TODO will this block API requests?
        })(),
    );
});
