export function Reviewing() {
  return (
    <div role="status" aria-live="polite" className="flex flex-col items-center py-16 text-center">
      <div className="relative flex size-28 items-center justify-center" aria-hidden="true">
        <span className="absolute inset-2 rounded-full bg-indigo-100 motion-safe:animate-ping motion-safe:[animation-duration:2s]" />
        <span className="relative flex size-24 items-center justify-center rounded-full bg-indigo-50 ring-1 ring-indigo-200">
          <span className="text-5xl motion-safe:animate-handshake">🤝</span>
        </span>
      </div>
      <h1 className="mt-8 text-xl font-semibold text-slate-900">Reviewing your application…</h1>
      <p className="mt-2 text-slate-600">This only takes a moment.</p>
    </div>
  );
}
