export function LoadingState({ label = 'Cargando…' }: { label?: string }) {
  return (
    <div className="state state--loading" role="status">
      {label}
    </div>
  );
}
