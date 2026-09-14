import { useMutation, useQueryClient } from '@tanstack/react-query';
import { verifyChain } from '../../api/evidence';
import { describeError } from '../../api/describeError';
import { describeVerifyResult } from './verifyChainMessages';
import type { VerifyChainResponse } from '../../api/types';

export function VerifyChainButton({
  evidenceId,
  onResult,
}: {
  evidenceId: number;
  onResult: (result: VerifyChainResponse) => void;
}) {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: () => verifyChain(evidenceId),
    onSuccess: (result) => {
      onResult(result);
      void queryClient.invalidateQueries({ queryKey: ['evidence', 'detail', evidenceId] });
      void queryClient.invalidateQueries({ queryKey: ['evidence', 'list'] });
    },
  });

  return (
    <div className="verify-chain">
      <button type="button" onClick={() => mutation.mutate()} disabled={mutation.isPending}>
        {mutation.isPending ? 'Verificando…' : 'Verificar cadena'}
      </button>
      {mutation.isSuccess && (
        <p role="status" className={mutation.data.isValid ? 'verify-chain__ok' : 'verify-chain__broken'}>
          {describeVerifyResult(mutation.data)}
        </p>
      )}
      {mutation.isError && (
        <p role="alert" className="verify-chain__broken">
          {describeError(mutation.error)}
        </p>
      )}
    </div>
  );
}
