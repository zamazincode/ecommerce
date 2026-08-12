"use client";

import { useRouter } from "next/navigation";
import { useState, type SubmitEvent } from "react";
import { Input } from "@/components/ui/input";
import { SearchIcon } from "lucide-react";

export function SearchBar() {
	const router = useRouter();
	const [value, setValue] = useState("");

	function handleSubmit(e: SubmitEvent) {
		e.preventDefault();
		const q = value.trim();
		if (q.length < 2) return;
		router.push(`/arama?q=${encodeURIComponent(q)}`);
	}

	return (
		<form onSubmit={handleSubmit} className="relative w-full max-w-md">
			<SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
			<Input
				value={value}
				onChange={(e) => setValue(e.target.value)}
				placeholder="Kitap, yazar, yayınevi ara…"
				className="pl-9"
			/>
		</form>
	);
}
