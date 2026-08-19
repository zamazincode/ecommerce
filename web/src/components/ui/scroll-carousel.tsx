"use client";

import * as React from "react";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

interface ScrollCarouselProps extends React.ComponentProps<"div"> {
	children: React.ReactNode;
}

/**
 * CSS scroll-snap tabanlı carousel (bkz. plan kararları) — kaydırma davranışı
 * mobilde native, JS'siz çalışır; ok butonları yalnızca masaüstünde ek kolaylık,
 * Embla gibi bir bağımlılık gerektirmez.
 */
function ScrollCarousel({
	children,
	className,
	...props
}: ScrollCarouselProps) {
	const trackRef = React.useRef<HTMLDivElement>(null);
	const [atStart, setAtStart] = React.useState(true);
	const [atEnd, setAtEnd] = React.useState(true);
	const [hasOverflow, setHasOverflow] = React.useState(false);

	const updateEdges = React.useCallback(() => {
		const el = trackRef.current;
		if (!el) return;
		setAtStart(el.scrollLeft <= 1);
		setAtEnd(el.scrollLeft + el.clientWidth >= el.scrollWidth - 1);
		setHasOverflow(el.scrollWidth > el.clientWidth + 1);
	}, []);

	// `ResizeObserver` — window `resize` tek başına yetmiyor: kategori şeridi
	// gibi `lg`'de grid'e dönüşen (taşma biten) track'lerde ya da içerik
	// görsellerin yüklenmesiyle boyut değiştiren carousel'lerde buton
	// durumu (disabled/hasOverflow) bayat kalıyordu — track'in kendi boyutunu
	// izlemek daha güvenilir.
	React.useEffect(() => {
		updateEdges();
		const el = trackRef.current;
		if (!el) return;
		const observer = new ResizeObserver(updateEdges);
		observer.observe(el);
		return () => observer.disconnect();
	}, [updateEdges]);

	function scrollByAmount(direction: 1 | -1) {
		const el = trackRef.current;
		if (!el) return;
		el.scrollBy({
			left: direction * el.clientWidth * 0.9,
			behavior: "smooth",
		});
	}

	const items = React.Children.map(children, (child) => {
		if (!React.isValidElement<{ className?: string }>(child)) return child;
		return React.cloneElement(child, {
			className: cn("shrink-0 snap-start", child.props.className),
		});
	});

	return (
		<div className="relative" {...props}>
			<div
				ref={trackRef}
				onScroll={updateEdges}
				className={cn(
					"flex snap-x snap-mandatory gap-4 overflow-x-auto scroll-smooth scrollbar-none [&::-webkit-scrollbar]:hidden py-2",
					className,
				)}
			>
				{items}
			</div>
			{hasOverflow ? (
				<>
					<Button
						type="button"
						variant="secondary"
						shape="pill"
						size="icon"
						onClick={() => scrollByAmount(-1)}
						disabled={atStart}
						aria-label="Geri kaydır"
						className={cn(
							"absolute top-1/2 left-0 hidden -translate-x-1/2 -translate-y-1/2 border-0 bg-background text-foreground shadow-pop hover:bg-background md:flex",
							atStart && "pointer-events-none opacity-0",
						)}
					>
						<ChevronLeftIcon />
					</Button>
					<Button
						type="button"
						variant="secondary"
						shape="pill"
						size="icon"
						onClick={() => scrollByAmount(1)}
						disabled={atEnd}
						aria-label="İleri kaydır"
						className={cn(
							"absolute top-1/2 right-0 hidden translate-x-1/2 -translate-y-1/2 border-0 bg-background text-foreground shadow-pop hover:bg-background md:flex",
							atEnd && "pointer-events-none opacity-0",
						)}
					>
						<ChevronRightIcon />
					</Button>
				</>
			) : null}
		</div>
	);
}

export { ScrollCarousel };
