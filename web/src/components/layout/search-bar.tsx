"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, type FormEvent } from "react";
import Link from "next/link";
import Image from "next/image";
import { Input } from "@/components/ui/input";
import { useSearchSuggestions } from "@/hooks/use-search-suggestions";
import { SearchIcon } from "lucide-react";

export function SearchBar() {
	const router = useRouter();
	const [value, setValue] = useState("");
	const [debounced, setDebounced] = useState("");
	const [error, setError] = useState<string | null>(null);
	const [isOpen, setIsOpen] = useState(false);
	const containerRef = useRef<HTMLDivElement>(null);

	// DEBOUNCE — 300ms yazma durunca öneri iste. Backend'e her karakterde
	// gitmek hem gereksiz trafik hem de Postgres FTS sorgusunu boşuna
	// tetikler (arama endpoint'i rate limit'e tabi DEĞİL ama yine de).
	useEffect(() => {
		const timeout = setTimeout(() => setDebounced(value), 300);
		return () => clearTimeout(timeout);
	}, [value]);

	const { data: suggestions } = useSearchSuggestions(debounced);

	// Dışarı tıklanınca kapat.
	useEffect(() => {
		function handleClickOutside(e: MouseEvent) {
			if (
				containerRef.current &&
				!containerRef.current.contains(e.target as Node)
			) {
				setIsOpen(false);
			}
		}
		document.addEventListener("mousedown", handleClickOutside);
		return () => document.removeEventListener("mousedown", handleClickOutside);
	}, []);

	function handleSubmit(e: FormEvent) {
		e.preventDefault();
		const q = value.trim();
		if (q.length < 2) {
			setError("Aramak için en az 2 karakter yazın.");
			return;
		}
		setError(null);
		setIsOpen(false);
		router.push(`/arama?q=${encodeURIComponent(q)}`);
	}

	return (
		<div ref={containerRef} className="relative w-full max-w-md">
			<form onSubmit={handleSubmit}>
				<SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
				<Input
					value={value}
					onChange={(e) => {
						setValue(e.target.value);
						setIsOpen(true);
						if (error) setError(null);
					}}
					onFocus={() => setIsOpen(true)}
					placeholder="Kitap, yazar, yayınevi ara…"
					className="pl-9"
				/>
			</form>
			{error ? (
				<p className="absolute mt-1 text-sm text-destructive">{error}</p>
			) : null}

			{isOpen && suggestions && suggestions.length > 0 ? (
				<ul className="absolute z-10 mt-1 w-full rounded-md border bg-popover shadow-lg">
					{suggestions.map((s) => (
						<li key={s.slug}>
							<Link
								href={`/urun/${s.slug}`}
								className="flex items-center gap-3 px-3 py-2 text-sm hover:bg-muted"
								onClick={() => setIsOpen(false)}
							>
								{s.imageUrl ? (
									<Image
										src={s.imageUrl}
										alt=""
										width={32}
										height={48}
										className="rounded object-cover"
									/>
								) : null}
								{s.name}
							</Link>
						</li>
					))}
				</ul>
			) : null}
		</div>
	);
}
