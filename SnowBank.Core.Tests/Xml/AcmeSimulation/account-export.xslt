<?xml version="1.0" encoding="UTF-8"?>
<!--
	Stage-A Acme end-to-end simulation: back-office export stylesheet, using the exact XPath patterns measured in the
	real corpus (nil-guarded collection reads, a bool-typed predicate over a collection item, an ArrayOf* element name
	read for a nested collection, and a polymorphic discriminator read over a collection of a base contract type).
	This stylesheet is intentionally profile-agnostic: it reads the DataContract-compat wire shape (no namespaces,
	explicit nil="true" markers, ordinal member names) produced identically by CrystalXml and by a live
	DataContractSerializer, so the SAME stylesheet transforms either wire.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:output method="html" encoding="UTF-8" omit-xml-declaration="yes" />

	<xsl:template match="/ClientAccount">
		<div class="account">

			<span class="owner">
				<xsl:text>Owner: </xsl:text>
				<xsl:choose>
					<xsl:when test="OwnerName[@nil='true']">
						<xsl:text>(none)</xsl:text>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="OwnerName" />
					</xsl:otherwise>
				</xsl:choose>
			</span>

			<span class="loans">
				<xsl:choose>
					<xsl:when test="Loans[not(@nil)]">
						<xsl:text>Loans: </xsl:text>
						<xsl:value-of select="count(Loans/Loan)" />
						<xsl:text>; Late: </xsl:text>
						<xsl:value-of select="count(Loans/Loan[IsLate = 'true'])" />
						<xsl:text>; OnTime: </xsl:text>
						<xsl:value-of select="count(Loans/Loan[IsLate = 'false'])" />
					</xsl:when>
					<xsl:otherwise>
						<xsl:text>Loans: none</xsl:text>
					</xsl:otherwise>
				</xsl:choose>
			</span>

			<span class="tag-groups">
				<xsl:choose>
					<xsl:when test="TagGroups[not(@nil)]">
						<xsl:text>TagGroups: </xsl:text>
						<xsl:value-of select="count(TagGroups/ArrayOfstring)" />
					</xsl:when>
					<xsl:otherwise>
						<xsl:text>TagGroups: none</xsl:text>
					</xsl:otherwise>
				</xsl:choose>
			</span>

			<span class="services">
				<xsl:choose>
					<xsl:when test="Services[not(@nil)]">
						<xsl:text>NonInsuranceServices: </xsl:text>
						<xsl:value-of select="count(Services/Service[@type != 'InsuranceService'])" />
					</xsl:when>
					<xsl:otherwise>
						<xsl:text>NonInsuranceServices: none</xsl:text>
					</xsl:otherwise>
				</xsl:choose>
			</span>

			<span class="insurance-services">
				<xsl:choose>
					<xsl:when test="Services[not(@nil)]">
						<xsl:text>InsuranceServices: </xsl:text>
						<xsl:value-of select="count(Services/Service[@type = 'InsuranceService'])" />
					</xsl:when>
					<xsl:otherwise>
						<xsl:text>InsuranceServices: none</xsl:text>
					</xsl:otherwise>
				</xsl:choose>
			</span>

		</div>
	</xsl:template>

</xsl:stylesheet>
