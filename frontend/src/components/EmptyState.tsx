import type { ReactNode } from 'react';

export function EmptyState({ message, action }: { message: string; action?: ReactNode }) {
  return (
    <div className="state state--empty">
      <p>{message}</p>
      {action}
    </div>
  );
}
