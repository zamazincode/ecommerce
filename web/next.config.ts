import type { NextConfig } from "next";
import createBundleAnalyzer from "@next/bundle-analyzer";

const withBundleAnalyzer = createBundleAnalyzer({
	enabled: process.env.ANALYZE === "true",
});

const nextConfig: NextConfig = {
	images: {
		remotePatterns: [
			{ protocol: "https", hostname: "i.dr.com.tr" },
			{ protocol: "https", hostname: "res.cloudinary.com" },
		],
	},
};

export default withBundleAnalyzer(nextConfig);
