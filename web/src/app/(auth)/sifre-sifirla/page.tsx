"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useSearchParams, useRouter } from "next/navigation";
import { useState } from "react";
import Link from "next/link";
import { AlertCircleIcon, EyeIcon, EyeOffIcon } from "lucide-react";
import { apiFetch, ApiError } from "@/lib/api/client";
import {
	resetPasswordSchema,
	type ResetPasswordInput,
} from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { EmptyState } from "@/components/ui/empty-state";
import Logo from "@/components/common/logo";

export default function ResetPasswordPage() {
	const router = useRouter();
	const searchParams = useSearchParams();
	const email = searchParams.get("email");
	const token = searchParams.get("token");
	const [serverError, setServerError] = useState<string | null>(null);
	const [showPassword, setShowPassword] = useState(false);

	const {
		register,
		handleSubmit,
		formState: { errors, isSubmitting },
	} = useForm<ResetPasswordInput>({
		resolver: zodResolver(resetPasswordSchema),
	});

	if (!email || !token) {
		return (
			<div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-card">
				<EmptyState
					icon={AlertCircleIcon}
					tone="danger"
					title="Geçersiz bağlantı"
					description="Bu şifre sıfırlama bağlantısı eksik ya da bozuk görünüyor."
					action={
						<Button
							render={<Link href="/sifremi-unuttum" />}
							nativeButton={false}
						>
							Tekrar İste
						</Button>
					}
				/>
			</div>
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
		<div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-card">
			<Link href="/" className="mb-6 flex justify-center">
				<Logo />
			</Link>

			<h1 className="mb-6 text-center text-lg font-semibold">
				Yeni Şifre Belirle
			</h1>

			<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
				{serverError ? (
					<p className="flex items-center gap-2 rounded-lg bg-destructive/10 p-3 text-sm text-destructive">
						<AlertCircleIcon className="size-4 shrink-0" />
						{serverError}
					</p>
				) : null}

				<FormField
					label="Yeni Şifre"
					htmlFor="newPassword"
					error={errors.newPassword?.message}
				>
					<div className="relative">
						<Input
							id="newPassword"
							type={showPassword ? "text" : "password"}
							{...register("newPassword")}
							className="pr-10"
						/>
						<Button
							type="button"
							variant="ghost"
							size="icon-sm"
							className="absolute top-1 right-1 bottom-1 h-auto text-muted-foreground"
							onClick={() => setShowPassword((prev) => !prev)}
							aria-label={
								showPassword ? "Şifreyi gizle" : "Şifreyi göster"
							}
						>
							{showPassword ? <EyeOffIcon /> : <EyeIcon />}
						</Button>
					</div>
				</FormField>

				<Button
					type="submit"
					className="w-full"
					disabled={isSubmitting}
				>
					{isSubmitting ? "Kaydediliyor…" : "Şifreyi Güncelle"}
				</Button>
			</form>
		</div>
	);
}
