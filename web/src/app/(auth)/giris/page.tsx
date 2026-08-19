"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter, useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import {
	AlertCircleIcon,
	ArrowRightIcon,
	EyeIcon,
	EyeOffIcon,
} from "lucide-react";
import { loginSchema, type LoginInput } from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { Separator } from "@/components/ui/separator";
import { toast } from "@/components/ui/toast";
import { useMergeCart } from "@/hooks/use-cart";
import Logo from "@/components/common/logo";

export default function LoginPage() {
	const router = useRouter();
	const searchParams = useSearchParams();
	const queryClient = useQueryClient();
	const mergeCart = useMergeCart();

	const [serverError, setServerError] = useState<string | null>(null);
	const [showPassword, setShowPassword] = useState(false);

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

		try {
			await mergeCart.mutateAsync();
		} catch {
			// Giriş zaten başarılı — sepet birleştirme başarısız olsa bile
			// kullanıcıyı burada tıkanmış bırakma, yönlendirmeye devam et.
			toast.add({
				title: "Sepetin birleştirilemedi",
				description: "Misafir sepetindeki ürünler eklenemedi.",
				type: "error",
			});
		}

		router.push(searchParams.get("returnUrl") ?? "/");
	}

	return (
		<div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-card">
			<Link href="/" className="mb-6 flex justify-center">
				<Logo />
			</Link>

			<div className="mb-6 text-center">
				<h1 className="text-lg font-semibold text-primary">
					D&R Kültür, Sanat ve Eğlence Dünyası
				</h1>
				<p className="mt-2 text-sm text-muted-foreground">
					Giriş yap ya da hemen üye ol; kültür, sanat ve eğlence
					dolu alışveriş deneyimini keşfet.
				</p>
			</div>

			<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
				{serverError ? (
					<p className="flex items-center gap-2 rounded-lg bg-destructive/10 p-3 text-sm text-destructive">
						<AlertCircleIcon className="size-4 shrink-0" />
						{serverError}
					</p>
				) : null}

				<FormField
					label="E-posta"
					htmlFor="email"
					error={errors.email?.message}
				>
					<Input
						id="email"
						type="email"
						{...register("email")}
						placeholder="E-posta Adresi"
					/>
				</FormField>

				<FormField
					label="Şifre"
					htmlFor="password"
					error={errors.password?.message}
				>
					<div className="relative">
						<Input
							id="password"
							type={showPassword ? "text" : "password"}
							{...register("password")}
							placeholder="Şifre"
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

				<Button type="submit" className="w-full" disabled={isSubmitting}>
					{isSubmitting ? "Giriş yapılıyor…" : "Giriş Yap"}
					<ArrowRightIcon />
				</Button>

				<Link
					href="/sifremi-unuttum"
					className="block text-center text-sm text-muted-foreground hover:underline"
				>
					Şifremi unuttum
				</Link>
			</form>

			<Separator className="my-6" />

			<p className="text-center text-sm text-muted-foreground">
				Hesabın yok mu?{" "}
				<Link
					href="/kayit"
					className="font-medium text-primary hover:underline"
				>
					Üye Ol
				</Link>
			</p>
		</div>
	);
}
