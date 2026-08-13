"use client";

import { useRouter } from "next/navigation";
import { useState, type SubmitEvent } from "react";
import { Input } from "@/components/ui/input";
import { SearchIcon } from "lucide-react";

export function SearchBar() {
	const router = useRouter();
	const [value, setValue] = useState("");
	const [error, setError] = useState<string | null>(null);

	function handleSubmit(e: SubmitEvent) {
		e.preventDefault();
		const q = value.trim();
		if (q.length < 2) {
			setError("Aramak için en az 2 karakter yazın.");
			return;
		}
		setError(null);
		router.push(`/arama?q=${encodeURIComponent(q)}`);
	}

	return (
		<form onSubmit={handleSubmit} className="relative w-full max-w-md">
			<div className="relative">
				<SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
				<Input
					value={value}
					onChange={(e) => {
						setValue(e.target.value);
						if (error) setError(null);
					}}
					placeholder="Kitap, yazar, yayınevi ara…"
					className="pl-9"
				/>
			</div>
			{error ? (
				<p className="absolute mt-1 text-sm text-destructive">{error}</p>
			) : null}
		</form>
	);
}
