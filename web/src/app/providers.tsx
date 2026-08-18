"use client";

import {
	QueryCache,
	QueryClient,
	QueryClientProvider,
} from "@tanstack/react-query";
import { useState } from "react";
import { Toaster, toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/client";

export function Providers({ children }: { children: React.ReactNode }) {
	// Sunucuya ilk istek atıldığında 1 kez instance oluşacak. Tarayıcı kapanana kadar çalışacak.
	const [queryClient] = useState(
		() =>
			new QueryClient({
				queryCache: new QueryCache({
					// Yalnızca useQuery hataları — useMutation'ın ayrı bir cache'i
					// var, Step 6/8/9/10'un yerel `catch` + `toast.add()`'ları bu
					// yüzden çift bildirim riski taşımadan aynen kalıyor.
					onError: (error, query) => {
						toast.add({
							title:
								(query.meta?.errorMessage as string | undefined) ??
								(error instanceof ApiError
									? "Veri yüklenemedi."
									: "Bağlantı hatası oluştu."),
							type: "error",
						});
					},
				}),
				defaultOptions: {
					queries: {
						staleTime: 60 * 1000,
						retry: 1,
						refetchOnWindowFocus: false,
					},
				},
			}),
	);

	return (
		<QueryClientProvider client={queryClient}>
			{children}
			<Toaster />
		</QueryClientProvider>
	);
}
