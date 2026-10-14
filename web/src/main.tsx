import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import './styles/app.css';
import { Layout } from './components/Layout';
import { Dashboard } from './pages/Dashboard';
import { Stock } from './pages/Stock';
import { Article } from './pages/Article';
import { Backlog } from './pages/Backlog';
import { Suppliers } from './pages/Suppliers';
import { Import } from './pages/Import';

const queryClient = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, refetchOnWindowFocus: false, retry: 1 } } });

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <Dashboard /> },
      { path: '/lager', element: <Stock /> },
      { path: '/lager/:number', element: <Article /> },
      { path: '/auftraege', element: <Backlog /> },
      { path: '/lieferanten', element: <Suppliers /> },
      { path: '/import', element: <Import /> },
      { path: '*', element: <p className="muted">Diese Seite gibt es nicht.</p> },
    ],
  },
]);

function render() {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
}

// First visit: get the workspace cookie with ONE request before the pages fire several in parallel –
// otherwise every cookie-less request would create its own workspace.
fetch('/api/workspace')
  .then(async (res) => { if (res.ok) queryClient.setQueryData(['workspace'], await res.json()); })
  .catch(() => undefined)
  .finally(render);
