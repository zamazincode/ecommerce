"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useSearchParams, useRouter } from "next/navigation";
import { useState } from "react";
import { apiFetch, ApiError } from "@/lib/api/client";
import {
	resetPasswordSchema,
	type ResetPasswordInput,
} from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";

export default function ResetPasswordPage() {
	const router = useRouter();
	const searchParams = useSearchParams();
	const email = searchParams.get("email");
	const token = searchParams.get("token");
	const [serverError, setServerError] = useState<string | null>(null);

	const {
		register,
		handleSubmit,
		formState: { errors, isSubmitting },
	} = useForm<ResetPasswordInput>({
		resolver: zodResolver(resetPasswordSchema),
	});

	if (!email || !token) {
		return (
			<main className="container-x py-16 text-center">
				<h1 className="text-xl font-semibold">Geçersiz bağlantı</h1>
				<p className="mt-2 text-muted-foreground">
					Bu şifre sıfırlama bağlantısı eksik ya da bozuk görünüyor.
				</p>
			</main>
		);
	}

	async function onSubmit(input: ResetPasswordInput) {
		setServerError(null);
		try {
			await apiFetch("auth/reset-password", {
				method: "POST",
				body: JSON.stringify({
					email,
					token,
					newPassword: input.newPassword,
				}),
			});
			router.push("/giris");
		} catch (error) {
			setServerError(
				error instanceof ApiError &&
					error.body &&
					typeof error.body === "object" &&
					"detail" in error.body
					? String(error.body.detail)
					: "Şifre sıfırlanamadı. Bağlantının süresi dolmuş olabilir.",
			);
		}
	}

	return (
		<main className="container-x flex min-h-[60vh] items-center justify-center py-16">
			<form
				onSubmit={handleSubmit(onSubmit)}
				className="w-full max-w-sm space-y-4"
			>
				<h1 className="text-xl font-semibold">Yeni Şifre Belirle</h1>
				{serverError ? (
					<p className="text-sm text-destructive">{serverError}</p>
				) : null}
				<div className="space-y-2">
					<Label htmlFor="newPassword">Yeni Şifre</Label>
					<Input
						id="newPassword"
						type="password"
						{...register("newPassword")}
					/>
					{errors.newPassword ? (
						<p className="text-sm text-destructive">
							{errors.newPassword.message}
						</p>
					) : null}
				</div>
				<Button
					type="submit"
					className="w-full"
					disabled={isSubmitting}
				>
					{isSubmitting ? "Kaydediliyor…" : "Şifreyi Güncelle"}
				</Button>
			</form>
		</main>
	);
}
