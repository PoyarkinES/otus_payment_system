import { render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';

it('renders in jsdom with jest-dom matchers', () => {
    render(<h1>Otus Payment System</h1>);

    expect(screen.getByRole('heading', { name: 'Otus Payment System' })).toBeInTheDocument();
});
