"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";

/**
 * Uzun metinleri (ürün açıklaması, yazar biyografisi) belli satırda kırpar,
 * "Devamını oku" ile açar. Tailwind'in class'ları statik string olarak
 * kalması gerektiğinden (JIT taraması), `lines` şablonlu değil sabit iki
 * değerden biri.
 */
export function ExpandableDescription({
	text,
	lines = 6,
}: {
	text: string;
	lines?: 4 | 6;
}) {
	const [expanded, setExpanded] = useState(false);

	return (
		<div>
			<p
				className={cn(
					"text-sm whitespace-pre-line text-muted-foreground",
					!expanded && (lines === 4 ? "line-clamp-4" : "line-clamp-6"),
				)}
			>
				{text}
			</p>
			<button
				type="button"
				onClick={() => setExpanded((e) => !e)}
				className="mt-1 text-sm font-medium text-primary hover:underline"
			>
				{expanded ? "Daha az göster" : "Devamını oku"}
			</button>
		</div>
	);
}
