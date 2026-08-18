import { expect, test, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { ErrorBoundary } from "@/components/error-boundary";

function Boom(): never {
	throw new Error("boom");
}

test("çocuk bileşen patlarsa fallback gösterilir", () => {
	vi.spyOn(console, "error").mockImplementation(() => {});

	render(
		<ErrorBoundary fallback={<p>Yüklenemedi</p>}>
			<Boom />
		</ErrorBoundary>,
	);

	expect(screen.getByText("Yüklenemedi")).toBeDefined();
});

test("hata yoksa children normal render olur", () => {
	render(
		<ErrorBoundary fallback={<p>Yüklenemedi</p>}>
			<p>Merhaba</p>
		</ErrorBoundary>,
	);

	expect(screen.getByText("Merhaba")).toBeDefined();
});
