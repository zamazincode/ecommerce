"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter, useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import Link from "next/link";
import { loginSchema, type LoginInput } from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { ChevronRight } from "lucide-react";

export default function LoginPage() {
	const router = useRouter();
	const searchParams = useSearchParams();
	const queryClient = useQueryClient();
	const [serverError, setServerError] = useState<string | null>(null);

	const {
		register,
		handleSubmit,
		formState: { errors, isSubmitting },
	} = useForm<LoginInput>({
		resolver: zodResolver(loginSchema),
	});

	async function onSubmit(input: LoginInput) {
		setServerError(null);

		const response = await fetch("/api/auth/login", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(input),
		});

		if (!response.ok) {
			setServerError("E-posta veya şifre hatalı.");
			return;
		}

		await queryClient.invalidateQueries({ queryKey: ["session"] });
		router.push(searchParams.get("returnUrl") ?? "/");
	}

	return (
		<main className="container-x flex min-h-[60vh] items-center justify-center py-16">
			<div className="max-w-xl w-fit border border-border flex flex-col items-center justify-center p-12 rounded-lg">
				<div className="mb-6">
					<h1 className="text-lg text-primary font-semibold text-center mb-2">
						D&R Kültür, Sanat ve Eğlence Dünyası
					</h1>
					<h2 className="text-center text-sm">
						Giriş yap ya da hemen üye ol; kültür, sanat ve eğlence
						dolu alışveriş deneyimini keşfet.
					</h2>
				</div>
				<form
					onSubmit={handleSubmit(onSubmit)}
					className="w-full space-y-4"
				>
					{serverError ? (
						<p className="text-sm text-destructive">
							{serverError}
						</p>
					) : null}

					<div className="space-y-2">
						<Label htmlFor="email">E-posta</Label>
						<Input
							id="email"
							type="email"
							{...register("email")}
							placeholder="E-posta Adresi"
						/>
						{errors.email ? (
							<p className="text-sm text-destructive">
								{errors.email.message}
							</p>
						) : null}
					</div>
					<div className="space-y-2">
						<Label htmlFor="password">Şifre</Label>
						<Input
							id="password"
							type="password"
							{...register("password")}
							placeholder="Şifre"
						/>
						{errors.password ? (
							<p className="text-sm text-destructive">
								{errors.password.message}
							</p>
						) : null}
					</div>

					<Button
						type="submit"
						className="w-full"
						disabled={isSubmitting}
					>
						{isSubmitting ? "Giriş yapılıyor…" : "Giriş Yap"}
						<ChevronRight className="text-white " />
					</Button>

					<div className="flex justify-between text-sm text-muted-foreground">
						<Link href="/kayit" className="hover:underline">
							Hesap oluştur
						</Link>
						<Link
							href="/sifremi-unuttum"
							className="hover:underline"
						>
							Şifremi unuttum
						</Link>
					</div>
				</form>
			</div>
		</main>
	);
}
