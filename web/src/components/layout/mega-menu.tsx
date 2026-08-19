"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ArrowRightIcon, FlameIcon, BadgePercentIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { components } from "@/types/api";

type CategoryTreeDto = components["schemas"]["CategoryTreeDto"];

/**
 * Kök kategorilerin altında hover'da açılan panel. Saf CSS `group`/`hover`
 * ile çalışıyor (bkz. plan kararları) — bir menü kütüphanesi/JS state
 * gerektirmiyor, yalnızca aktif sayfayı vurgulamak için `usePathname`
 * istiyor, o yüzden client bileşen.
 */
export function MegaMenu({ tree }: { tree: CategoryTreeDto[] }) {
	const pathname = usePathname();

	return (
		<nav className="relative hidden border-b bg-background lg:block z-0">
			<div className="container-x flex items-center gap-1">
				{tree.map((category) => {
					const href = `/kategori/${category.slug}`;
					const children = category.children ?? [];

					return (
						<div key={category.id} className="group/menu">
							<Link
								href={href}
								className={cn(
									"flex h-11 items-center rounded-lg px-3 text-sm font-medium hover:bg-primary-soft hover:text-primary",
									pathname === href && "text-primary",
								)}
							>
								{category.name}
							</Link>

							{children.length > 0 ? (
								<div className="invisible absolute inset-x-0 top-full z-10 border-b bg-background opacity-0 shadow-pop transition-all duration-150 group-hover/menu:visible group-hover/menu:opacity-100">
									<div className="container-x grid grid-cols-4 gap-x-8 gap-y-2 py-6">
										<div className="col-span-3 grid grid-cols-3 gap-x-8 gap-y-4">
											{children.map((child) => (
												<div key={child.id}>
													<Link
														href={`/kategori/${child.slug}`}
														className="text-sm font-medium hover:text-primary"
													>
														{child.name}
													</Link>
													{child.children &&
													child.children.length >
														0 ? (
														<ul className="mt-2 space-y-1.5">
															{child.children
																.slice(0, 6)
																.map(
																	(
																		grandchild,
																	) => (
																		<li
																			key={
																				grandchild.id
																			}
																		>
																			<Link
																				href={`/kategori/${grandchild.slug}`}
																				className="text-sm text-muted-foreground hover:text-primary"
																			>
																				{
																					grandchild.name
																				}
																			</Link>
																		</li>
																	),
																)}
														</ul>
													) : null}
												</div>
											))}
										</div>
										<div className="rounded-xl bg-primary-soft p-5">
											<p className="font-heading font-semibold text-primary">
												{category.name}
											</p>
											<Button
												variant="soft"
												shape="pill"
												size="sm"
												className="mt-4"
												render={<Link href={href} />}
												nativeButton={false}
											>
												Tümünü Gör
												<ArrowRightIcon />
											</Button>
										</div>
									</div>
								</div>
							) : null}
						</div>
					);
				})}

				<Link
					href="/kategori/kitap"
					className="flex h-11 items-center gap-1.5 rounded-lg px-3 text-sm font-medium hover:bg-primary-soft hover:text-primary"
				>
					<FlameIcon className="size-4" />
					Çok Satanlar
				</Link>
				<Link
					href="/kategori/kitap"
					className="flex h-11 items-center gap-1.5 rounded-lg px-3 text-sm font-medium text-red hover:bg-red-soft"
				>
					<BadgePercentIcon className="size-4" />
					İndirimdekiler
				</Link>
			</div>
		</nav>
	);
}
