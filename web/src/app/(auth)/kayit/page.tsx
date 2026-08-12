"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useState } from "react";
import Link from "next/link";
import { registerSchema, type RegisterInput } from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { ChevronRight } from "lucide-react";

export default function RegisterPage() {
	const router = useRouter();
	const [serverError, setServerError] = useState<string | null>(null);

	const {
		register,
		handleSubmit,
		formState: { errors, isSubmitting },
	} = useForm<RegisterInput>({
		resolver: zodResolver(registerSchema),
	});

	async function onSubmit(input: RegisterInput) {
		setServerError(null);

		const response = await fetch("/api/auth/register", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(input),
		});

		if (!response.ok) {
			const body = await response.json().catch(() => null);
			setServerError(
				body?.detail ?? body?.message ?? "Kayıt oluşturulamadı.",
			);
			return;
		}

		router.push("/");
		router.refresh();
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
					className="w-full max-w-sm space-y-4"
				>
					{serverError ? (
						<p className="text-sm text-destructive">
							{serverError}
						</p>
					) : null}

					<div className="grid grid-cols-2 gap-4">
						<div className="space-y-2">
							<Label htmlFor="firstName">Ad</Label>
							<Input id="firstName" {...register("firstName")} />
							{errors.firstName ? (
								<p className="text-sm text-destructive">
									{errors.firstName.message}
								</p>
							) : null}
						</div>
						<div className="space-y-2">
							<Label htmlFor="lastName">Soyad</Label>
							<Input id="lastName" {...register("lastName")} />
							{errors.lastName ? (
								<p className="text-sm text-destructive">
									{errors.lastName.message}
								</p>
							) : null}
						</div>
					</div>
					<div className="space-y-2">
						<Label htmlFor="email">E-posta</Label>
						<Input id="email" type="email" {...register("email")} />
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
						/>
						{errors.password ? (
							<p className="text-sm text-destructive">
								{errors.password.message}
							</p>
						) : null}
						<p className="text-xs text-muted-foreground">
							En az 8 karakter, büyük/küçük harf ve rakam
							içermeli.
						</p>
					</div>

					<Button
						type="submit"
						className="w-full"
						disabled={isSubmitting}
					>
						{isSubmitting ? "Oluşturuluyor…" : "Hesap Oluştur"}
						<ChevronRight className="text-white " />
					</Button>

					<p className="text-center text-sm text-muted-foreground">
						Zaten hesabın var mı?{" "}
						<Link href="/giris" className="underline">
							Giriş yap
						</Link>
					</p>
				</form>
			</div>
		</main>
	);
}
