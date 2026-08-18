"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { apiFetch } from "@/lib/api/client";
import {
	forgotPasswordSchema,
	type ForgotPasswordInput,
} from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";

export default function ForgotPasswordPage() {
	const [sent, setSent] = useState(false);
	const {
		register,
		handleSubmit,
		formState: { errors, isSubmitting },
	} = useForm<ForgotPasswordInput>({
		resolver: zodResolver(forgotPasswordSchema),
	});

	async function onSubmit(input: ForgotPasswordInput) {
		try {
			await apiFetch("auth/forgot-password", {
				method: "POST",
				body: JSON.stringify(input),
			});
			setSent(true);
		} catch {
			toast.add({
				title: "Gönderilemedi",
				description: "Bir hata oluştu, lütfen tekrar dene.",
				type: "error",
			});
		}
	}

	if (sent) {
		return (
			<main className="container-x py-16 text-center">
				<h1 className="text-xl font-semibold">E-postanı kontrol et</h1>
				<p className="mt-2 text-muted-foreground">
					Kayıtlıysa, şifre sıfırlama bağlantısını içeren bir e-posta
					gönderdik.
				</p>
			</main>
		);
	}

	return (
		<main className="container-x flex min-h-[60vh] items-center justify-center py-16">
			<form
				onSubmit={handleSubmit(onSubmit)}
				className="w-full max-w-sm space-y-4"
			>
				<h1 className="text-xl font-semibold">Şifremi Unuttum</h1>
				<div className="space-y-2">
					<Label htmlFor="email">E-posta</Label>
					<Input
						id="email"
						type="email"
						{...register("email")}
						placeholder="Eposta Adresi"
					/>
					{errors.email ? (
						<p className="text-sm text-destructive">
							{errors.email.message}
						</p>
					) : null}
				</div>
				<Button
					type="submit"
					className="w-full"
					disabled={isSubmitting}
				>
					{isSubmitting
						? "Gönderiliyor…"
						: "Sıfırlama Bağlantısı Gönder"}
				</Button>
			</form>
		</main>
	);
}
