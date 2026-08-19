"use client";

import { CornerDownRightIcon, FolderTreeIcon, PencilIcon, PlusIcon } from "lucide-react";
import { useAdminCategories } from "@/hooks/use-admin-categories";
import { CategoryFormDialog } from "@/components/admin/category-form-dialog";
import { categoryDepth } from "@/lib/admin/category-tree";
import { PageHeader } from "@/components/admin/page-header";
import { RowActions } from "@/components/admin/row-actions";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { TableSkeleton } from "@/components/ui/data-state";
import { EmptyState } from "@/components/ui/empty-state";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";

export default function AdminCategoriesPage() {
	const { data: categories, isLoading } = useAdminCategories();

	return (
		<div className="space-y-6">
			<PageHeader
				title="Kategoriler"
				actions={
					categories ? (
						<CategoryFormDialog
							categories={categories}
							trigger={
								<Button>
									<PlusIcon />
									Yeni Kategori
								</Button>
							}
						/>
					) : undefined
				}
			/>

			<Card className="overflow-hidden p-0">
				{isLoading || !categories ? (
					<TableSkeleton rows={10} cols={4} />
				) : categories.length === 0 ? (
					<EmptyState
						icon={FolderTreeIcon}
						title="Henüz kategori yok"
					/>
				) : (
					<Table>
						<TableHeader className="bg-muted/50">
							<TableRow className="hover:bg-transparent">
								<TableHead>Ad</TableHead>
								<TableHead>Ürün Sayısı</TableHead>
								<TableHead>Durum</TableHead>
								<TableHead className="text-right">İşlemler</TableHead>
							</TableRow>
						</TableHeader>
						<TableBody>
							{categories.map((category) => {
								const depth = categoryDepth(category, categories);
								return (
									<TableRow key={category.id}>
										<TableCell>
											<span
												className="inline-flex items-center gap-1.5"
												style={{ paddingInlineStart: depth * 20 }}
											>
												{depth > 0 ? (
													<CornerDownRightIcon className="size-3.5 text-muted-foreground" />
												) : null}
												<span
													className={
														depth === 0 ? "font-medium" : undefined
													}
												>
													{category.name}
												</span>
											</span>
										</TableCell>
										<TableCell>
											<Badge
												variant="secondary"
												className="tabular-nums"
											>
												{category.productCount}
											</Badge>
										</TableCell>
										<TableCell>
											<Badge
												variant={
													category.isActive
														? "success"
														: "secondary"
												}
											>
												{category.isActive ? "Aktif" : "Pasif"}
											</Badge>
										</TableCell>
										<TableCell className="text-right">
											{/* NOT: `CategoryFormDialog` `trigger`'ı Base UI'ın
											    `DialogTrigger render={trigger}` ile klonluyor —
											    bu, `RowAction`'ın kendi Tooltip sarmalayıcısına
											    ekstra prop'ları (onClick/aria-*) ileten bir yapı
											    değil (bkz. row-actions.tsx'teki `confirm` notu),
											    o yüzden burada Tooltip'siz düz bir `Button`
											    kullanılıyor. */}
											<RowActions>
												<CategoryFormDialog
													category={category}
													categories={categories}
													trigger={
														<Button
															variant="ghost"
															size="icon-sm"
															aria-label="Düzenle"
														>
															<PencilIcon />
														</Button>
													}
												/>
											</RowActions>
										</TableCell>
									</TableRow>
								);
							})}
						</TableBody>
					</Table>
				)}
			</Card>
		</div>
	);
}
