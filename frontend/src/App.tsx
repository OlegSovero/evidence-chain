import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { Layout } from './Layout';
import { InboxPage } from './features/evidence/InboxPage';
import { DetailPage } from './features/evidence/DetailPage';
import { PendingInboxPage } from './features/transfers/PendingInboxPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
    },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<Navigate to="/evidencias" replace />} />
              <Route path="evidencias" element={<InboxPage />} />
              <Route path="evidencias/:id" element={<DetailPage />} />
              <Route path="transferencias" element={<PendingInboxPage />} />
              <Route path="*" element={<Navigate to="/evidencias" replace />} />
            </Route>
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
