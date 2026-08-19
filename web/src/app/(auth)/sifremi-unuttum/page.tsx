"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import Link from "next/link";
import { MailCheckIcon } from "lucide-react";
import { apiFetch } from "@/lib/api/client";
import {
	forgotPasswordSchema,
	type ForgotPasswordInput,
} from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { toast } from "@/components/ui/toast";
import Logo from "@/components/common/logo";

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
			<div className="w-full max-w-md rounded-2xl border bg-card p-8 text-center shadow-card">
				<div className="mx-auto mb-4 grid size-14 place-items-center rounded-full bg-success-soft text-success">
					<MailCheckIcon className="size-7" />
				</div>
				<h1 className="text-lg font-semibold">
					E-postanı kontrol et
				</h1>
				<p className="mt-2 text-sm text-muted-foreground">
					Kayıtlıysa, şifre sıfırlama bağlantısını içeren bir
					e-posta gönderdik.
				</p>
				<Link
					href="/giris"
					className="mt-6 inline-block text-sm font-medium text-primary hover:underline"
				>
					Girişe dön
				</Link>
			</div>
		);
	}

	return (
		<div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-card">
			<Link href="/" className="mb-6 flex justify-center">
				<Logo />
			</Link>

			<div className="mb-6 text-center">
				<h1 className="text-lg font-semibold">Şifremi Unuttum</h1>
				<p className="mt-2 text-sm text-muted-foreground">
					E-posta adresini gir, sana bir sıfırlama bağlantısı
					gönderelim.
				</p>
			</div>

			<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
				<FormField
					label="E-posta"
					htmlFor="email"
					error={errors.email?.message}
				>
					<Input
						id="email"
						type="email"
						{...register("email")}
						placeholder="Eposta Adresi"
					/>
				</FormField>
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
		</div>
	);
}
