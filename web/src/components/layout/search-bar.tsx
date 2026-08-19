"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, type FormEvent } from "react";
import Link from "next/link";
import Image from "next/image";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
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
		setValue("");
		router.push(`/arama?q=${encodeURIComponent(q)}`);
	}

	const trimmed = value.trim();

	return (
		<div ref={containerRef} className="relative w-full">
			<form onSubmit={handleSubmit} className="relative">
				<Input
					inputSize="lg"
					value={value}
					onChange={(e) => {
						setValue(e.target.value);
						setIsOpen(true);
						if (error) setError(null);
					}}
					onFocus={() => setIsOpen(true)}
					placeholder="Kitap, yazar, yayınevi ara…"
					// `!` (important) şart — Input'un `data-[size=lg]:px-5` kuralı
					// bir öznitelik seçicisi taşıdığı için düz `pr-14`'ten daha
					// yüksek özgüllükte, sıradan bir override onu geçemiyor.
					className="pr-14!"
				/>
				<Button
					type="submit"
					size="icon-lg"
					shape="pill"
					aria-label="Ara"
					className="absolute top-1 right-1 bottom-1 h-auto"
				>
					<SearchIcon />
				</Button>
			</form>

			{error ? (
				<p className="mt-1.5 text-xs text-destructive">{error}</p>
			) : null}

			{isOpen && suggestions && suggestions.length > 0 ? (
				<ul className="absolute z-30 mt-1.5 w-full rounded-2xl border-0 bg-popover p-2 shadow-pop ring-1 ring-foreground/10">
					{suggestions.map((s) => (
						<li key={s.slug}>
							<Link
								href={`/urun/${s.slug}`}
								className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm hover:bg-primary-soft"
								onClick={() => {
									setIsOpen(false);
									setValue("");
								}}
							>
								{s.imageUrl ? (
									<Image
										src={s.imageUrl}
										alt={s.name}
										width={32}
										height={48}
										className="rounded-md object-cover"
									/>
								) : null}
								<span className="line-clamp-1">{s.name}</span>
							</Link>
						</li>
					))}
					{trimmed.length >= 2 ? (
						<li>
							<Link
								href={`/arama?q=${encodeURIComponent(trimmed)}`}
								onClick={() => {
									setIsOpen(false);
									setValue("");
								}}
								className="block rounded-xl px-3 py-2.5 text-sm font-medium text-primary hover:bg-primary-soft"
							>
								&quot;{trimmed}&quot; için tüm sonuçları gör
							</Link>
						</li>
					) : null}
				</ul>
			) : null}
		</div>
	);
}
